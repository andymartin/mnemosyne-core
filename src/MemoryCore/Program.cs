using System.IO.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Controllers;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Mcp;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Persistence;
using Mnemosyne.Core.Services;
using Neo4j.Driver;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Mnemosyne.Core;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.WriteIndented = true;
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.Converters.Clear();
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
            });

        // Register the globally configured JsonSerializerOptions for other services to use
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);

        builder.Services.AddSignalR();

        // Configure Neo4j driver
        builder.Services.AddSingleton<IDriver>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            return Neo4jService.ConfigureNeo4jDriver(configuration);
        });

        // Register Neo4jService
        builder.Services.AddSingleton<Neo4jService>();

        // Configure EmbeddingService options
        builder.Services.Configure<EmbeddingServiceOptions>(
            builder.Configuration.GetSection(EmbeddingServiceOptions.SectionName));

        // Configure HttpClient for Embedding Service with resilience policies
        builder.Services.AddHttpClient<IEmbeddingService, HttpEmbeddingService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmbeddingServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddPolicyHandler((serviceProvider, _) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmbeddingServiceOptions>>().Value;
            return HttpPolicyExtensions
                .HandleTransientHttpError() // HttpRequestException, 5XX and 408 status codes
                .Or<TimeoutRejectedException>() // Thrown by Polly's TimeoutPolicy
                .WaitAndRetryAsync(
                    options.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<HttpEmbeddingService>>();
                        logger.LogWarning(
                            "Retrying request to Embedding Service after {RetryAttempt} attempts due to {StatusCode}: {Message}",
                            retryAttempt,
                            outcome.Result?.StatusCode,
                            outcome.Exception?.Message);
                    });
        })
        .AddPolicyHandler((serviceProvider, _) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmbeddingServiceOptions>>().Value;
            return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(options.TimeoutSeconds));
        });

        // Add repository and services
        builder.Services.AddSingleton<IMemorygramRepository, Neo4jMemorygramRepository>();
        builder.Services.AddSingleton<IMemorygramService, MemorygramService>();
        builder.Services.AddSingleton<IMemoryQueryService, MemoryQueryService>();
        builder.Services.AddSingleton<IQueryMemoryTool, QueryMemoryTool>();

        // Configure PipelineStorageOptions
        builder.Services.Configure<PipelineStorageOptions>(
            builder.Configuration.GetSection(PipelineStorageOptions.SectionName));

        // Register FileSystem
        builder.Services.TryAddSingleton<IFileSystem, FileSystem>();

        // Register Pipelines Repository and Service
        builder.Services.AddSingleton<IPipelinesRepository, FilePipelinesRepository>();
        builder.Services.AddSingleton<IPipelinesService, PipelinesService>();

        // Register MCP server
        builder.Services.AddMcpServer()
            .WithToolsFromAssembly();

        // Add Swagger/OpenAPI
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Mnemosyne Core API", Version = "v1" });

            // Include XML comments for API documentation
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });

        var app = builder.Build();

        // Initialize Neo4j schema
        using (var scope = app.Services.CreateScope())
        {
            var neo4jService = scope.ServiceProvider.GetRequiredService<Neo4jService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                var result = await neo4jService.InitializeSchemaAsync();
                if (result.IsFailed)
                {
                    logger.LogError("Failed to initialize Neo4j schema: {Errors}", string.Join(", ", result.Errors.Select(e => e.Message)));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while initializing Neo4j schema");
            }
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Mnemosyne Core API V1");
            });
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.UseWebSockets();
        app.MapControllers();
        app.MapHub<ChatHub>("/ws/chat");
        await app.RunAsync();
    }
}

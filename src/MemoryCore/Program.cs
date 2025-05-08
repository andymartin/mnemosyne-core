using MemoryCore.Services;
using MemoryCore.Interfaces;
using MemoryCore.Models;
using MemoryCore.Persistence;
using Neo4j.Driver;
using FluentResults;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Net.Http;
using ModelContextProtocol.Server;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Neo4j driver
builder.Services.AddSingleton<IDriver>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return Neo4jService.ConfigureNeo4jDriver(configuration);
});

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
builder.Services.TryAddSingleton<IMemorygramRepository, Neo4jMemorygramRepository>();
builder.Services.TryAddSingleton<IMemorygramService, MemorygramService>();
builder.Services.TryAddSingleton<IMemoryQueryService, MemoryQueryService>();

// Register MCP server
builder.Services.AddMcpServer()
    .WithToolsFromAssembly();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "MemoryCore API", Version = "v1" });
    
    // Include XML comments for API documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MemoryCore API V1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
    }
}

using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Mcp;
using FluentResults;
using Mnemosyne.Core.Persistence;
using Mnemosyne.Core.Services;
using Neo4j.Driver;
using NSubstitute;

namespace MemoryCore.Tests.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Mnemosyne.Core.Program>
{
    private readonly Dictionary<string, string> _configValues = new Dictionary<string, string>();
    private readonly Neo4jContainerFixture? _neo4jFixture;
    // private readonly EmbeddingServiceContainerFixture? _embeddingFixture; // Disabled Testcontainer for embedding

    public CustomWebApplicationFactory()
    {
    }

    public CustomWebApplicationFactory(
        Neo4jContainerFixture? neo4jFixture = null)
        // EmbeddingServiceContainerFixture? embeddingFixture = null) // Disabled Testcontainer for embedding
    {
        _neo4jFixture = neo4jFixture;
        // _embeddingFixture = embeddingFixture; // Disabled Testcontainer for embedding
    }

    public CustomWebApplicationFactory WithPipelineStoragePath(string path)
    {
        var pipelineStoragePathKey = $"PipelineStorage:{nameof(PipelineStorageOptions.StoragePath)}";
        _configValues[pipelineStoragePathKey] = path;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);


        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(_configValues!);

            if (_neo4jFixture != null)
            {
                _configValues["Neo4j:Uri"] = _neo4jFixture.GetBoltUri();
                _configValues["Neo4j:Username"] = "neo4j";
                _configValues["Neo4j:Password"] = "password";
            }
        });

        builder.ConfigureServices(services =>
        {
            // Use real file system for integration tests
            services.RemoveAll<IFileSystem>();
            services.AddSingleton<IFileSystem, FileSystem>();

            services.RemoveAll<PipelineStorageOptions>();
            services.Configure<PipelineStorageOptions>(options =>
            {
                // Point to the actual pipelines directory in the test project
                var testProjectPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                options.StoragePath = System.IO.Path.Combine(testProjectPath!, "pipelines");
            });

            services.RemoveAll<IDriver>();
            if (_neo4jFixture != null)
            {
                services.AddSingleton<IDriver>(sp => GraphDatabase.Driver(_neo4jFixture.GetBoltUri(), AuthTokens.Basic("neo4j", "neo4j")));
            }
            else
            {
                services.AddSingleton<IDriver>(sp => Substitute.For<IDriver>());
            }

            // Mock IEmbeddingService for integration tests
            var embedding = Enumerable.Range(0, 1024).Select(i => 0.1f + i * 0.1f).ToArray();
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService>(sp =>
            {
                var mockEmbeddingService = Substitute.For<IEmbeddingService>();
                mockEmbeddingService.GetEmbeddingAsync(Arg.Any<string>())
                    .Returns(Task.FromResult(Result.Ok(embedding)));
                return mockEmbeddingService;
            });

            // Ensure ILanguageModelService is registered with a mock for integration tests
            services.RemoveAll<ILanguageModelService>();
            services.AddSingleton<ILanguageModelService>(sp =>
            {
                var mockLanguageModelService = Substitute.For<ILanguageModelService>();
                var reformulations = Enum.GetValues<MemoryReformulationType>()
                                         .ToDictionary(
                                             type => type.ToString(),
                                             type => $"Mocked reform. for {type}");
                var mockJsonResponse = JsonSerializer.Serialize(reformulations);
                mockLanguageModelService.GenerateCompletionAsync(Arg.Any<ChatCompletionRequest>(), Arg.Any<LanguageModelType>())
                    .Returns(Task.FromResult(Result.Ok(mockJsonResponse)));
                return mockLanguageModelService;
            });

            // Add repository and services
            services.RemoveAll<IMemorygramRepository>();
            services.AddSingleton<IMemorygramRepository, Neo4jMemorygramRepository>();

            services.RemoveAll<IMemorygramService>();
            services.AddSingleton<IMemorygramService, MemorygramService>();

            services.RemoveAll<IMemoryQueryService>();
            services.AddSingleton<IMemoryQueryService, MemoryQueryService>();

            services.RemoveAll<IQueryMemoryTool>();
            services.AddSingleton<IQueryMemoryTool, QueryMemoryTool>();

            // Register Pipelines Repository and Service
            services.RemoveAll<IPipelinesRepository>();
            services.AddSingleton<IPipelinesRepository, FilePipelinesRepository>();

            services.RemoveAll<IPipelinesService>();
            services.AddSingleton<IPipelinesService, PipelinesService>();

            // Register Semantic Reformulator
            services.RemoveAll<ISemanticReformulator>();
            services.AddSingleton<ISemanticReformulator, SemanticReformulator>();

            // Configure JSON serialization to match the Program.cs configuration
            services.AddControllers()
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
            services.RemoveAll<JsonSerializerOptions>();
            services.AddSingleton<JsonSerializerOptions>(sp => sp.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);

            services.AddMcpServer()
                .WithToolsFromAssembly();
        });
    }

}

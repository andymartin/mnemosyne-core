using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Mcp;
using Mnemosyne.Core.Persistence;
using Mnemosyne.Core.Services;
using Mnemosyne.Core.Tests.Fixtures;
using Neo4j.Driver;
using NSubstitute;

namespace Mnemosyne.Core.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string> _configValues = new Dictionary<string, string>();
        private readonly Neo4jContainerFixture? _neo4jFixture;
        private readonly EmbeddingServiceContainerFixture? _embeddingFixture;

        public MockFileSystem FileSystem { get; }

        public CustomWebApplicationFactory()
        {
            FileSystem = new MockFileSystem();
        }

        public CustomWebApplicationFactory(
            Neo4jContainerFixture? neo4jFixture = null,
            EmbeddingServiceContainerFixture? embeddingFixture = null)
        {
            _neo4jFixture = neo4jFixture;
            _embeddingFixture = embeddingFixture;
            FileSystem = new MockFileSystem();
        }

        public CustomWebApplicationFactory WithPipelineStoragePath(string path)
        {
            var pipelineStoragePathKey = $"PipelineStorage:{nameof(PipelineStorageOptions.StoragePath)}";
            _configValues[pipelineStoragePathKey] = path;
            FileSystem.Directory.CreateDirectory(path); // Ensure directory exists in mock
            return this;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            var pipelineStoragePathKey = $"PipelineStorage:{nameof(PipelineStorageOptions.StoragePath)}";
            if (!_configValues.ContainsKey(pipelineStoragePathKey))
            {
                 var defaultPath = FileSystem.Path.Combine(FileSystem.Path.GetTempPath(), "TestPipelineManifests", Guid.NewGuid().ToString());
                 _configValues[pipelineStoragePathKey] = defaultPath;
            }

            FileSystem.Directory.CreateDirectory(_configValues[pipelineStoragePathKey]);

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
                services.RemoveAll<IFileSystem>();
                services.AddSingleton<IFileSystem>(FileSystem);

                services.RemoveAll<PipelineStorageOptions>();
                services.Configure<PipelineStorageOptions>(options =>
                {
                    options.StoragePath = _configValues[pipelineStoragePathKey];
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

                services.RemoveAll<IEmbeddingService>();
                if (_embeddingFixture != null)
                {
                    services.AddHttpClient<IEmbeddingService, HttpEmbeddingService>((serviceProvider, client) =>
                    {
                        client.BaseAddress = new Uri(_embeddingFixture.GetConnectionString());
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });
                }
                else
                {
                    services.AddHttpClient<IEmbeddingService, HttpEmbeddingService>((serviceProvider, client) =>
                    {
                        client.BaseAddress = new Uri("http://localhost");
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });
                }


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
}

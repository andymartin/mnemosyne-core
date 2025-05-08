using MemoryCore.Interfaces;
using MemoryCore.Mcp;
using MemoryCore.Models;
using MemoryCore.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace MemoryCore.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly Neo4jContainer _neo4jContainer = new Neo4jBuilder().WithImage("neo4j:latest").Build();
        private readonly EmbeddingServiceContainer _embeddingServiceContainer = new EmbeddingServiceContainer();
        private readonly Dictionary<string, string> _configValues = new Dictionary<string, string>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // We'll update this dictionary after containers are started
                config.AddInMemoryCollection(_configValues!);
            });

            builder.ConfigureServices(services =>
            {
                // Remove the original Neo4j driver registration
                var driverDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDriver));
                if (driverDescriptor != null)
                {
                    services.Remove(driverDescriptor);
                }

                // Add a new Neo4j driver connected to the Testcontainer
                services.AddSingleton<IDriver>(sp =>
                {
                    return GraphDatabase.Driver(_neo4jContainer.GetConnectionString(), AuthTokens.Basic("neo4j", "neo4j"));
                });

                // Configure HttpClient for Embedding Service with the testcontainer
                services.RemoveAll<IEmbeddingService>();
                services.AddHttpClient<IEmbeddingService, HttpEmbeddingService>((serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<EmbeddingServiceOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                });

                // Register MCP services needed for integration tests
                services.AddScoped<IMemoryQueryService, MemoryQueryService>();
                services.AddScoped<QueryMemoryTool>();

                // Register MCP server
                services.AddMcpServer()
                    .WithToolsFromAssembly();
            });
        }

        public async Task InitializeAsync()
        {
            await _embeddingServiceContainer.InitializeAsync();
            _configValues["EmbeddingService:BaseUrl"] = _embeddingServiceContainer.GetConnectionString();
            _configValues["EmbeddingService:TimeoutSeconds"] = "30";
            _configValues["EmbeddingService:MaxRetryAttempts"] = "3";
            _configValues["EmbeddingService:RetryDelayMilliseconds"] = "200";

            await _neo4jContainer.StartAsync();
        }

        public new async Task DisposeAsync()
        {
            await _embeddingServiceContainer.DisposeAsync();
            await _neo4jContainer.DisposeAsync();
        }
    }
}

using MemoryCore.Interfaces;
using MemoryCore.Mcp;
using MemoryCore.Models;
using MemoryCore.Services;
using MemoryCore.Tests.Fixtures;
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
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string> _configValues = new Dictionary<string, string>();
        private Neo4jContainerFixture _neo4jFixture;
        private EmbeddingServiceContainerFixture _embeddingFixture;

        // Constructor that accepts fixture instances
        public CustomWebApplicationFactory(
            Neo4jContainerFixture neo4jFixture,
            EmbeddingServiceContainerFixture embeddingFixture)
        {
            _neo4jFixture = neo4jFixture;
            _embeddingFixture = embeddingFixture;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(_configValues!);
                
                // If fixtures are provided, use their connection details
                if (_neo4jFixture != null)
                {
                    _configValues["Neo4j:Uri"] = _neo4jFixture.GetBoltUri();
                    _configValues["Neo4j:Username"] = "neo4j";
                    _configValues["Neo4j:Password"] = "password";
                }
            });

            builder.ConfigureServices(services =>
            {
                // Remove the original Neo4j driver registration
                var driverDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDriver));
                if (driverDescriptor != null)
                {
                    services.Remove(driverDescriptor);
                }

                // Add a new Neo4j driver connected to the Testcontainer if fixture is provided
                if (_neo4jFixture != null)
                {
                    services.AddSingleton<IDriver>(sp =>
                    {
                        return GraphDatabase.Driver(_neo4jFixture.GetBoltUri(), AuthTokens.Basic("neo4j", "neo4j"));
                    });
                }

                // Configure HttpClient for Embedding Service with the testcontainer
                services.RemoveAll<IEmbeddingService>();
                
                // Directly use the connection string from the fixture
                if (_embeddingFixture != null)
                {
                    services.AddHttpClient<IEmbeddingService, HttpEmbeddingService>((serviceProvider, client) =>
                    {
                        client.BaseAddress = new Uri(_embeddingFixture.GetConnectionString());
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });
                }

                // Register MCP services needed for integration tests
                services.AddScoped<IMemoryQueryService, MemoryQueryService>();
                services.AddScoped<QueryMemoryTool>();

                // Register MCP server
                services.AddMcpServer()
                    .WithToolsFromAssembly();
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace MemoryCore.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly Neo4jContainer _neo4jContainer = new Neo4jBuilder().Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Add test-specific configuration if needed
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
            });
        }

        public async Task InitializeAsync()
        {
            await _neo4jContainer.StartAsync();
        }

        public new async Task DisposeAsync()
        {
            await _neo4jContainer.DisposeAsync();
        }
    }
}
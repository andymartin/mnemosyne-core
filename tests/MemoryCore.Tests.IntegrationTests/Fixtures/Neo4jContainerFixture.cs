namespace MemoryCore.Tests.IntegrationTests.Fixtures;

public class Neo4jContainerFixture : IAsyncLifetime
{
    public Testcontainers.Neo4j.Neo4jContainer Container { get; }

    public Neo4jContainerFixture()
    {
        Container = new Testcontainers.Neo4j.Neo4jBuilder()
            .WithImage("neo4j:latest")
            .Build();
    }

    public string GetBoltUri() => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.StopAsync();
        await Container.DisposeAsync();
    }
}

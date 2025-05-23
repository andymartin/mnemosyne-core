namespace MemoryCore.Tests.IntegrationTests.Fixtures
{
    [CollectionDefinition("TestContainerCollection")]
    public class TestContainerCollection : 
        ICollectionFixture<Neo4jContainerFixture>, 
        ICollectionFixture<EmbeddingServiceContainerFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}

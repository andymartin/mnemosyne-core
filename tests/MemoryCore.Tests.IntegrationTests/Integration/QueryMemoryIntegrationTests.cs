using MemoryCore.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Shouldly;
using Xunit.Abstractions;

namespace MemoryCore.Tests.IntegrationTests.Integration;

[Trait("Category", "Integration")]
[Collection("TestContainerCollection")]
public class QueryMemoryIntegrationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _scopedServiceProvider;
    private readonly IMemoryQueryService _memoryQueryService;
    private readonly Neo4jContainerFixture _neo4jFixture;
    private readonly EmbeddingServiceContainerFixture _embeddingFixture;

    public QueryMemoryIntegrationTests(
        ITestOutputHelper output,
        Neo4jContainerFixture neo4jFixture,
        EmbeddingServiceContainerFixture embeddingFixture)
    {
        _output = output;
        _neo4jFixture = neo4jFixture;
        _embeddingFixture = embeddingFixture;

        // Create a factory that uses the fixtures
        _factory = new CustomWebApplicationFactory(neo4jFixture, embeddingFixture);
        _client = _factory.CreateClient();

        // Create a scope to resolve scoped services
        _scope = _factory.Services.CreateScope();
        _scopedServiceProvider = _scope.ServiceProvider;
        _memoryQueryService = _scopedServiceProvider.GetRequiredService<IMemoryQueryService>();
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }

    [Fact]
    public async Task QueryMemoryService_EndToEnd_ReturnsExpectedResults()
    {
        // Arrange
        var queryText = "Test query for integration test";
        var topK = 3;
        var input = new McpQueryInput(queryText, topK);

        // First create some test memorygrams in Neo4j to query against
        var memorygrams = await CreateTestMemorygramsAsync();
        _output.WriteLine($"Created {memorygrams.Count} test memorygrams");

        // Act
        _output.WriteLine($"Executing QueryAsync with query: '{queryText}' and topK: {topK}");
        var result = await _memoryQueryService.QueryAsync(input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Status.ShouldBe("success");
        result.Value.Results.ShouldNotBeNull();
        result.Value.Message.ShouldBeNull();

        // We should get results back (up to topK)
        result.Value.Results.Count.ShouldBeLessThanOrEqualTo(topK);
        result.Value.Results.Count.ShouldBeGreaterThan(0);

        // Verify result structure
        foreach (var item in result.Value.Results)
        {
            item.Id.ShouldNotBe(Guid.Empty);
            item.Content.ShouldNotBeNullOrWhiteSpace();
            item.Score.ShouldBeGreaterThan(0);
            item.CreatedAt.ShouldNotBe(default);
            item.UpdatedAt.ShouldNotBe(default);
        }

        // Results should be ordered by score (highest first)
        for (int i = 0; i < result.Value.Results.Count - 1; i++)
        {
            result.Value.Results[i].Score.ShouldBeGreaterThanOrEqualTo(result.Value.Results[i + 1].Score);
        }
    }

    [Fact]
    public async Task QueryMemoryService_WithInvalidTopK_ReturnsError()
    {
        // Arrange
        var queryText = "Test query with invalid topK";
        var topK = 0; // Invalid value
        var input = new McpQueryInput(queryText, topK);

        // Act
        _output.WriteLine($"Executing QueryAsync with query: '{queryText}' and invalid topK: {topK}");
        var result = await _memoryQueryService.QueryAsync(input);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().Message.ShouldContain("greater than 0");
    }

    [Fact]
    public async Task QueryMemoryService_WithEmptyQueryText_ReturnsError()
    {
        // Arrange
        var queryText = "";
        var topK = 5;
        var input = new McpQueryInput(queryText, topK);

        // Act
        _output.WriteLine($"Executing QueryAsync with empty query text");
        var result = await _memoryQueryService.QueryAsync(input);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().Message.ShouldContain("empty");
    }

    [Fact]
    public async Task QueryMemoryService_VerifyEmbeddingServiceIntegration()
    {
        // Arrange
        var queryText = "Test query to verify embedding service integration";
        var topK = 3;
        var input = new McpQueryInput(queryText, topK);

        // Create test memorygrams
        await CreateTestMemorygramsAsync();

        // Get the embedding service to verify the embedding generation
        var embeddingService = _scopedServiceProvider.GetRequiredService<IEmbeddingService>();

        // Act - Get the embedding directly from the service
        var embeddingResult = await embeddingService.GetEmbeddingAsync(queryText);

        // Assert - Verify the embedding service returns a valid embedding
        embeddingResult.IsSuccess.ShouldBeTrue();
        var queryVector = embeddingResult.Value;
        queryVector.ShouldNotBeNull();
        queryVector.Length.ShouldBeGreaterThan(0);

        // Now test the full query flow
        var result = await _memoryQueryService.QueryAsync(input);

        // Verify the query was successful
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("success");

        // Verify results have similarity scores
        if (result.Value.Results != null && result.Value.Results.Count > 0)
        {
            foreach (var item in result.Value.Results)
            {
                item.Score.ShouldBeGreaterThan(0);
            }
        }
    }

    [Fact]
    public async Task QueryMemoryService_WithSpecificTopK_LimitsResults()
    {
        // Arrange
        var queryText = "Test query with specific topK value";
        var topK = 3; // Specific value to test limit
        var input = new McpQueryInput(queryText, topK);

        // Create test memorygrams - more than our requested topK
        await CreateTestMemorygramsAsync();

        // Act
        _output.WriteLine($"Executing QueryAsync with query: '{queryText}' and specific topK: {topK}");
        var result = await _memoryQueryService.QueryAsync(input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Status.ShouldBe("success");
        result.Value.Results.ShouldNotBeNull();

        // We should get exactly the number of results we requested
        result.Value.Results.Count.ShouldBe(topK);
    }

    private async Task<List<Guid>> CreateTestMemorygramsAsync()
    {
        var createdIds = new List<Guid>();
        var contents = new[]
        {
            "This is a test memorygram for vector similarity search",
            "Another test memorygram with different content",
            "A third memorygram for testing query functionality",
            "Vector search should find this memorygram based on similarity",
            "Completely unrelated content that shouldn't match well"
        };

        var repository = _scopedServiceProvider.GetRequiredService<IMemorygramRepository>();
        var embeddingService = _scopedServiceProvider.GetRequiredService<IEmbeddingService>();

        foreach (var content in contents)
        {

            var embeddingResult = await embeddingService.GetEmbeddingAsync(content);
            if (embeddingResult.IsFailed)
            {
                _output.WriteLine($"Failed to get embedding for content: {content}");
                continue;
            }

            var memorygram = new Memorygram(
                Guid.NewGuid(),
                content,
                MemorygramType.UserInput,
                embeddingResult.Value,
                embeddingResult.Value,
                embeddingResult.Value,
                embeddingResult.Value,
                "IntegrationTest",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            var result = await repository.CreateOrUpdateMemorygramAsync(memorygram);
            if (result.IsSuccess)
            {
                createdIds.Add(result.Value.Id);
            }
        }

        // Give Neo4j a moment to index the vectors
        await Task.Delay(500);

        return createdIds;
    }
}

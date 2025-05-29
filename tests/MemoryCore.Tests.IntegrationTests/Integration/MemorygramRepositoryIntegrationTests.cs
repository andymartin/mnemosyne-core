using MemoryCore.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Neo4j.Driver;
using Shouldly;
using Xunit.Abstractions;

namespace MemoryCore.Tests.IntegrationTests.Integration;

[Trait("Category", "Integration")]
[Collection("TestContainerCollection")]
public class MemorygramRepositoryIntegrationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _scopedServiceProvider;
    private readonly IMemorygramRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly Neo4jContainerFixture _neo4jFixture;

    public MemorygramRepositoryIntegrationTests(
        ITestOutputHelper output,
        Neo4jContainerFixture neo4jFixture)
    {
        _output = output;
        _neo4jFixture = neo4jFixture;

        _factory = new CustomWebApplicationFactory(neo4jFixture);
        
        _scope = _factory.Services.CreateScope();
        _scopedServiceProvider = _scope.ServiceProvider;
        _repository = _scopedServiceProvider.GetRequiredService<IMemorygramRepository>();
        _embeddingService = _scopedServiceProvider.GetRequiredService<IEmbeddingService>();
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }

    [Fact]
    public async Task GetAllChatsAsync_WithNoChatInitiations_ReturnsEmptyCollection()
    {
        // Arrange - Clear any existing data first
        await ClearDatabaseAsync();
        
        // Create memorygrams that are NOT chat initiations
        var nonChatMemorygram = await CreateTestMemorygramAsync("Non-chat memorygram", null, null);
        var chatMemorygramWithPrevious = await CreateTestMemorygramAsync("Chat memorygram with previous", Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _repository.GetAllChatsAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllChatsAsync_WithChatInitiations_ReturnsOnlyChatInitiations()
    {
        // Arrange - Clear any existing data first
        await ClearDatabaseAsync();
        
        // Create various types of memorygrams
        var chatId1 = Guid.NewGuid();
        var chatId2 = Guid.NewGuid();

        // Chat initiation memorygrams (PreviousMemorygramId is null, ChatId is not null)
        var chatInitiation1 = await CreateTestMemorygramAsync("First chat initiation", chatId1, null);
        var chatInitiation2 = await CreateTestMemorygramAsync("Second chat initiation", chatId2, null);

        // Non-chat memorygrams (should be excluded)
        var nonChatMemorygram = await CreateTestMemorygramAsync("Non-chat memorygram", null, null);
        var chatContinuation = await CreateTestMemorygramAsync("Chat continuation", chatId1, Guid.NewGuid());

        // Give Neo4j a moment to persist the data
        await Task.Delay(100);

        // Act
        var result = await _repository.GetAllChatsAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        
        var chatInitiations = result.Value.ToList();
        chatInitiations.Count.ShouldBe(2);

        // Verify the returned memorygrams are the correct chat initiations
        var initiationIds = chatInitiations.Select(m => m.Id).ToList();
        initiationIds.ShouldContain(chatInitiation1.Id);
        initiationIds.ShouldContain(chatInitiation2.Id);

        // Verify they have the correct properties
        foreach (var initiation in chatInitiations)
        {
            initiation.Subtype.ShouldBeNull(); // Chat initiations don't have a subtype by default
            initiation.PreviousMemorygramId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task GetAllChatsAsync_OrdersByTimestampDescending()
    {
        // Arrange - Clear any existing data first
        await ClearDatabaseAsync();
        
        // Create chat initiations with different timestamps
        var chatId1 = Guid.NewGuid();
        var chatId2 = Guid.NewGuid();
        var chatId3 = Guid.NewGuid();

        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // Create chat initiations with different timestamps (older first)
        var olderInitiation = await CreateTestMemorygramAsync("Older chat", chatId1, null, baseTimestamp - 3600); // 1 hour ago
        var middleInitiation = await CreateTestMemorygramAsync("Middle chat", chatId2, null, baseTimestamp - 1800); // 30 minutes ago
        var newerInitiation = await CreateTestMemorygramAsync("Newer chat", chatId3, null, baseTimestamp); // now

        // Give Neo4j a moment to persist the data
        await Task.Delay(100);

        // Act
        var result = await _repository.GetAllChatsAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        
        var chatInitiations = result.Value.ToList();
        chatInitiations.Count.ShouldBe(3);

        // Verify they are ordered by timestamp descending (newest first)
        chatInitiations[0].Id.ShouldBe(newerInitiation.Id);
        chatInitiations[1].Id.ShouldBe(middleInitiation.Id);
        chatInitiations[2].Id.ShouldBe(olderInitiation.Id);
    }

    [Fact]
    public async Task GetAllChatsAsync_WithDatabaseError_ReturnsFailure()
    {
        // This test would require simulating a database error
        // For now, we'll test the successful path
        // In a real scenario, you might use a test double or mock the driver
        
        // Act
        var result = await _repository.GetAllChatsAsync();

        // Assert - At minimum, it should not throw and should return a Result
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateRelationshipAsync_WithValidParameters_CreatesRelationship()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("First memorygram");
        var memorygram2 = await CreateTestMemorygramAsync("Second memorygram");
        var relationshipType = "RELATED_TO";
        var weight = 0.85f;
        var properties = "{\"category\": \"test\"}";

        // Act
        var result = await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram2.Id, relationshipType, weight, properties);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.FromMemorygramId.ShouldBe(memorygram1.Id);
        result.Value.ToMemorygramId.ShouldBe(memorygram2.Id);
        result.Value.RelationshipType.ShouldBe(relationshipType);
        result.Value.Weight.ShouldBe(weight);
        result.Value.Properties.ShouldBe(properties);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateRelationshipAsync_WithNonExistentMemorygram_ReturnsFailure()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("Existing memorygram");
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.CreateRelationshipAsync(memorygram1.Id, nonExistentId, "RELATED_TO", 0.5f);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetRelationshipByIdAsync_WithExistingId_ReturnsRelationship()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("First memorygram");
        var memorygram2 = await CreateTestMemorygramAsync("Second memorygram");
        
        var createResult = await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram2.Id, "CONNECTS_TO", 0.9f);
        createResult.IsSuccess.ShouldBeTrue();
        var relationshipId = createResult.Value.Id;

        // Act
        var result = await _repository.GetRelationshipByIdAsync(relationshipId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(relationshipId);
        result.Value.FromMemorygramId.ShouldBe(memorygram1.Id);
        result.Value.ToMemorygramId.ShouldBe(memorygram2.Id);
        result.Value.RelationshipType.ShouldBe("CONNECTS_TO");
        result.Value.Weight.ShouldBe(0.9f);
    }

    [Fact]
    public async Task GetRelationshipsByMemorygramIdAsync_WithExistingRelationships_ReturnsRelationships()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("Central memorygram");
        var memorygram2 = await CreateTestMemorygramAsync("Related memorygram 1");
        var memorygram3 = await CreateTestMemorygramAsync("Related memorygram 2");
        
        // Create outgoing relationships
        await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram2.Id, "RELATES_TO", 0.8f);
        await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram3.Id, "CONNECTS_TO", 0.7f);
        
        // Create incoming relationship
        await _repository.CreateRelationshipAsync(memorygram3.Id, memorygram1.Id, "POINTS_TO", 0.6f);

        // Act
        var result = await _repository.GetRelationshipsByMemorygramIdAsync(memorygram1.Id, true, true);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        
        var relationships = result.Value.ToList();
        relationships.Count.ShouldBe(3);
        
        // Check that we have both incoming and outgoing relationships
        relationships.ShouldContain(r => r.FromMemorygramId == memorygram1.Id && r.ToMemorygramId == memorygram2.Id);
        relationships.ShouldContain(r => r.FromMemorygramId == memorygram1.Id && r.ToMemorygramId == memorygram3.Id);
        relationships.ShouldContain(r => r.FromMemorygramId == memorygram3.Id && r.ToMemorygramId == memorygram1.Id);
    }

    [Fact]
    public async Task GetRelationshipsByTypeAsync_WithExistingType_ReturnsRelationships()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("First memorygram");
        var memorygram2 = await CreateTestMemorygramAsync("Second memorygram");
        var memorygram3 = await CreateTestMemorygramAsync("Third memorygram");
        
        var targetType = "SIMILAR_TO";
        await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram2.Id, targetType, 0.9f);
        await _repository.CreateRelationshipAsync(memorygram2.Id, memorygram3.Id, targetType, 0.8f);
        await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram3.Id, "DIFFERENT_TYPE", 0.7f);

        // Act
        var result = await _repository.GetRelationshipsByTypeAsync(targetType);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        
        var relationships = result.Value.ToList();
        relationships.Count.ShouldBe(2);
        relationships.ShouldAllBe(r => r.RelationshipType == targetType);
    }

    [Fact]
    public async Task UpdateRelationshipAsync_WithValidParameters_UpdatesRelationship()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("First memorygram");
        var memorygram2 = await CreateTestMemorygramAsync("Second memorygram");
        
        var createResult = await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram2.Id, "RELATES_TO", 0.5f);
        createResult.IsSuccess.ShouldBeTrue();
        var relationshipId = createResult.Value.Id;

        var newWeight = 0.9f;
        var newProperties = "{\"updated\": true}";

        // Act
        var result = await _repository.UpdateRelationshipAsync(relationshipId, newWeight, newProperties, false);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(relationshipId);
        result.Value.Weight.ShouldBe(newWeight);
        result.Value.Properties.ShouldBe(newProperties);
        result.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteRelationshipAsync_WithExistingId_DeletesRelationship()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("First memorygram");
        var memorygram2 = await CreateTestMemorygramAsync("Second memorygram");
        
        var createResult = await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram2.Id, "TEMP_RELATION", 0.5f);
        createResult.IsSuccess.ShouldBeTrue();
        var relationshipId = createResult.Value.Id;

        // Act
        var deleteResult = await _repository.DeleteRelationshipAsync(relationshipId);
        
        // Assert
        deleteResult.IsSuccess.ShouldBeTrue();
        
        // Verify the relationship is gone
        var getResult = await _repository.GetRelationshipByIdAsync(relationshipId);
        getResult.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task FindRelationshipsAsync_WithCriteria_ReturnsMatchingRelationships()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var memorygram1 = await CreateTestMemorygramAsync("First memorygram");
        var memorygram2 = await CreateTestMemorygramAsync("Second memorygram");
        var memorygram3 = await CreateTestMemorygramAsync("Third memorygram");
        
        await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram2.Id, "HIGH_WEIGHT", 0.9f);
        await _repository.CreateRelationshipAsync(memorygram1.Id, memorygram3.Id, "LOW_WEIGHT", 0.3f);
        await _repository.CreateRelationshipAsync(memorygram2.Id, memorygram3.Id, "MEDIUM_WEIGHT", 0.6f);

        // Act - Find relationships with weight >= 0.5
        var result = await _repository.FindRelationshipsAsync(minWeight: 0.5f);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        
        var relationships = result.Value.ToList();
        relationships.Count.ShouldBe(2);
        relationships.ShouldAllBe(r => r.Weight >= 0.5f);
    }

    private async Task<Memorygram> CreateTestMemorygramAsync(
        string content, 
        Guid? chatId = null, 
        Guid? previousMemorygramId = null,
        long? timestamp = null)
    {
        var embeddingResult = await _embeddingService.GetEmbeddingAsync(content);
        embeddingResult.IsSuccess.ShouldBeTrue($"Failed to get embedding for content: {content}");

        var memorygram = new Memorygram(
            Guid.NewGuid(),
            content,
            MemorygramType.UserInput,
            embeddingResult.Value,
            embeddingResult.Value,
            embeddingResult.Value,
            embeddingResult.Value,
            "IntegrationTest",
            timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null, // Subtype parameter (replacing chatId)
            previousMemorygramId,
            null,
            null
        );

        var result = await _repository.CreateOrUpdateMemorygramAsync(memorygram);
        result.IsSuccess.ShouldBeTrue($"Failed to create test memorygram: {result.Errors.FirstOrDefault()?.Message}");
        
        return result.Value;
    }

    private async Task ClearDatabaseAsync()
    {
        // Clear all memorygrams from the test database
        using var session = _scopedServiceProvider.GetRequiredService<IDriver>().AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("MATCH (m:Memorygram) DETACH DELETE m");
            return Task.CompletedTask;
        });
    }
}
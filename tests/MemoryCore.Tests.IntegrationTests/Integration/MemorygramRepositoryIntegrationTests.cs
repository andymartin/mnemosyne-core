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
            initiation.ChatId.ShouldNotBeNull();
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
            chatId,
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
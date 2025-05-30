using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using NSubstitute;
using Shouldly;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace MemoryCore.Tests.UnitTests.Performance;

public class ChatServicePerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IResponderService _mockResponderService;
    private readonly IMemorygramService _mockMemorygramService;
    private readonly IMemoryQueryService _mockMemoryQueryService;
    private readonly ILogger<ChatService> _mockLogger;
    private readonly ChatService _chatService;

    public ChatServicePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockResponderService = Substitute.For<IResponderService>();
        _mockMemorygramService = Substitute.For<IMemorygramService>();
        _mockMemoryQueryService = Substitute.For<IMemoryQueryService>();
        _mockLogger = Substitute.For<ILogger<ChatService>>();

        _chatService = new ChatService(
            _mockResponderService,
            _mockMemorygramService,
            _mockMemoryQueryService,
            _mockLogger);
    }

    [Fact]
    public async Task GetChatHistoryAsync_ShouldPerformWell_WithLargeDataset()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        const int messageCount = 1000;

        var experience = CreateTestMemorygram(experienceId, MemorygramType.Experience, "Large chat experience", chatId.ToString());

        // Create large dataset of relationships
        var relationships = new List<GraphRelationship>();
        var memorygrams = new List<Memorygram>();

        for (int i = 0; i < messageCount; i++)
        {
            var messageId = Guid.NewGuid();
            var messageType = i % 2 == 0 ? MemorygramType.UserInput : MemorygramType.AssistantResponse;
            var message = CreateTestMemorygram(messageId, messageType, $"Message {i}", timestamp: i);
            
            memorygrams.Add(message);
            relationships.Add(new GraphRelationship(
                Guid.NewGuid(), experienceId, messageId, "ROOT_OF", 1.0f, 
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }

        // Mock the service calls
        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>
            {
                new GraphRelationship(Guid.NewGuid(), experienceId, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            }));

        _mockMemorygramService.GetMemorygramByIdAsync(experienceId)
            .Returns(Result.Ok(experience));

        _mockMemorygramService.GetRelationshipsByMemorygramIdAsync(experienceId, false, true)
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Setup individual memorygram retrievals
        foreach (var memorygram in memorygrams)
        {
            _mockMemorygramService.GetMemorygramByIdAsync(memorygram.Id)
                .Returns(Result.Ok(memorygram));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _chatService.GetChatHistoryAsync(chatId);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(messageCount);
        
        // Performance assertion - should complete within reasonable time
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        elapsedMs.ShouldBeLessThan(5000); // 5 seconds max for 1000 messages
        
        _output.WriteLine($"Retrieved {messageCount} messages in {elapsedMs}ms ({elapsedMs / (double)messageCount:F2}ms per message)");
        
        // Verify ordering is maintained
        for (int i = 1; i < result.Value.Count; i++)
        {
            result.Value[i].Timestamp.ShouldBeGreaterThanOrEqualTo(result.Value[i - 1].Timestamp);
        }
    }

    [Fact]
    public async Task GetAllChatExperiencesAsync_ShouldPerformWell_WithManyExperiences()
    {
        // Arrange
        const int experienceCount = 500;
        var experiences = new List<Memorygram>();
        var relationships = new List<GraphRelationship>();

        for (int i = 0; i < experienceCount; i++)
        {
            var experienceId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var experience = CreateTestMemorygram(experienceId, MemorygramType.Experience, $"Experience {i}", chatId.ToString());
            experiences.Add(experience);

            relationships.Add(new GraphRelationship(
                Guid.NewGuid(), experienceId, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f,
                DateTimeOffset.UtcNow.AddMinutes(-i), DateTimeOffset.UtcNow.AddMinutes(-i)));
        }

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Setup individual experience retrievals
        foreach (var experience in experiences)
        {
            _mockMemorygramService.GetMemorygramByIdAsync(experience.Id)
                .Returns(Result.Ok(experience));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _chatService.GetAllChatExperiencesAsync();
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(experienceCount);
        
        // Performance assertion
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        elapsedMs.ShouldBeLessThan(3000); // 3 seconds max for 500 experiences
        
        _output.WriteLine($"Retrieved {experienceCount} experiences in {elapsedMs}ms ({elapsedMs / (double)experienceCount:F2}ms per experience)");
        
        // Verify ordering (most recent first)
        for (int i = 1; i < result.Value.Count; i++)
        {
            result.Value[i].CreatedAt.ShouldBeLessThanOrEqualTo(result.Value[i - 1].CreatedAt);
        }
    }

    [Fact]
    public async Task GetExperienceForChatAsync_ShouldPerformWell_WithManyRelationships()
    {
        // Arrange
        var targetChatId = Guid.NewGuid();
        var targetExperienceId = Guid.NewGuid();
        const int relationshipCount = 1000;

        var relationships = new List<GraphRelationship>();
        var experiences = new List<Memorygram>();

        // Create many relationships with different chat IDs
        for (int i = 0; i < relationshipCount; i++)
        {
            var experienceId = i == relationshipCount / 2 ? targetExperienceId : Guid.NewGuid();
            var chatId = i == relationshipCount / 2 ? targetChatId : Guid.NewGuid();
            
            var experience = CreateTestMemorygram(experienceId, MemorygramType.Experience, $"Experience {i}", chatId.ToString());
            experiences.Add(experience);

            relationships.Add(new GraphRelationship(
                Guid.NewGuid(), experienceId, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Setup individual experience retrievals
        foreach (var experience in experiences)
        {
            _mockMemorygramService.GetMemorygramByIdAsync(experience.Id)
                .Returns(Result.Ok(experience));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _chatService.GetExperienceForChatAsync(targetChatId);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(targetExperienceId);
        
        // Performance assertion
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        elapsedMs.ShouldBeLessThan(2000); // 2 seconds max to find target among 1000 relationships
        
        _output.WriteLine($"Found target experience among {relationshipCount} relationships in {elapsedMs}ms");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task GetChatHistoryAsync_ShouldScaleLinearly_WithMessageCount(int messageCount)
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();

        var experience = CreateTestMemorygram(experienceId, MemorygramType.Experience, "Scalability test experience", chatId.ToString());

        var relationships = new List<GraphRelationship>();
        var memorygrams = new List<Memorygram>();

        for (int i = 0; i < messageCount; i++)
        {
            var messageId = Guid.NewGuid();
            var messageType = i % 2 == 0 ? MemorygramType.UserInput : MemorygramType.AssistantResponse;
            var message = CreateTestMemorygram(messageId, messageType, $"Message {i}", timestamp: i);
            
            memorygrams.Add(message);
            relationships.Add(new GraphRelationship(
                Guid.NewGuid(), experienceId, messageId, "ROOT_OF", 1.0f, 
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>
            {
                new GraphRelationship(Guid.NewGuid(), experienceId, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            }));

        _mockMemorygramService.GetMemorygramByIdAsync(experienceId)
            .Returns(Result.Ok(experience));

        _mockMemorygramService.GetRelationshipsByMemorygramIdAsync(experienceId, false, true)
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        foreach (var memorygram in memorygrams)
        {
            _mockMemorygramService.GetMemorygramByIdAsync(memorygram.Id)
                .Returns(Result.Ok(memorygram));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _chatService.GetChatHistoryAsync(chatId);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(messageCount);
        
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var msPerMessage = elapsedMs / (double)messageCount;
        
        // Should maintain reasonable per-message performance
        msPerMessage.ShouldBeLessThan(5.0); // Less than 5ms per message
        
        _output.WriteLine($"Messages: {messageCount}, Time: {elapsedMs}ms, Per message: {msPerMessage:F2}ms");
    }

    [Fact]
    public async Task GetChatHistoryAsync_ShouldHandleEmptyResults_Efficiently()
    {
        // Arrange
        var chatId = Guid.NewGuid();

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>()));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _chatService.GetChatHistoryAsync(chatId);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        
        // Should be very fast for empty results
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        elapsedMs.ShouldBeLessThan(100); // Less than 100ms for empty result
        
        _output.WriteLine($"Empty result returned in {elapsedMs}ms");
    }

    private static Memorygram CreateTestMemorygram(
        Guid id, 
        MemorygramType type, 
        string content, 
        string? subtype = null, 
        long timestamp = 0)
    {
        return new Memorygram(
            Id: id,
            Content: content,
            Type: type,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: "PerformanceTest",
            Timestamp: timestamp,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Subtype: subtype
        );
    }
}
using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MemoryCore.Tests.UnitTests.Services;

public class ChatServiceTests
{
    private readonly IResponderService _mockResponderService;
    private readonly IMemorygramService _mockMemorygramService;
    private readonly IMemoryQueryService _mockMemoryQueryService;
    private readonly ILogger<ChatService> _mockLogger;
    private readonly ChatService _chatService;

    public ChatServiceTests()
    {
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
    public async Task ProcessUserMessageAsync_ShouldReturnSuccess_WhenResponderServiceSucceeds()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var userText = "Hello, world!";
        var expectedResponse = new ResponseResult
        {
            Response = "Test response",
            SystemPrompt = "Test system prompt"
        };
        
        _mockResponderService.ProcessRequestAsync(Arg.Any<PipelineExecutionRequest>())
            .Returns(Result.Ok(expectedResponse));

        // Act
        var result = await _chatService.ProcessUserMessageAsync(chatId, userText);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedResponse);
        
        await _mockResponderService.Received(1).ProcessRequestAsync(
            Arg.Is<PipelineExecutionRequest>(req => 
                req.UserInput == userText &&
                req.SessionMetadata.ContainsKey("chatId") &&
                req.SessionMetadata["chatId"].Equals(chatId)));
    }

    [Fact]
    public async Task ProcessUserMessageAsync_ShouldReturnFailure_WhenResponderServiceFails()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var userText = "Hello, world!";
        var errorMessage = "Test error";
        
        _mockResponderService.ProcessRequestAsync(Arg.Any<PipelineExecutionRequest>())
            .Returns(Result.Fail<ResponseResult>(errorMessage));

        // Act
        var result = await _chatService.ProcessUserMessageAsync(chatId, userText);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("Failed to process message with ResponderService"));
    }

    [Fact]
    public async Task GetChatHistoryAsync_ShouldReturnOrderedHistory_WhenExperienceExists()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var userMessageId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();

        var experience = CreateTestMemorygram(experienceId, MemorygramType.Experience, "Test experience", chatId.ToString());
        var userMessage = CreateTestMemorygram(userMessageId, MemorygramType.UserInput, "Hello", timestamp: 1000);
        var assistantMessage = CreateTestMemorygram(assistantMessageId, MemorygramType.AssistantResponse, "Hi there", timestamp: 2000);

        var relationships = new List<GraphRelationship>
        {
            new GraphRelationship(Guid.NewGuid(), experienceId, userMessageId, "ROOT_OF", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new GraphRelationship(Guid.NewGuid(), experienceId, assistantMessageId, "ROOT_OF", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        // Mock GetExperienceForChatAsync
        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>
            {
                new GraphRelationship(Guid.NewGuid(), experienceId, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            }));

        _mockMemorygramService.GetMemorygramByIdAsync(experienceId)
            .Returns(Result.Ok(experience));

        // Mock GetChatHistoryAsync relationships
        _mockMemorygramService.GetRelationshipsByMemorygramIdAsync(experienceId, false, true)
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        _mockMemorygramService.GetMemorygramByIdAsync(userMessageId)
            .Returns(Result.Ok(userMessage));

        _mockMemorygramService.GetMemorygramByIdAsync(assistantMessageId)
            .Returns(Result.Ok(assistantMessage));

        // Act
        var result = await _chatService.GetChatHistoryAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].ShouldBe(userMessage); // Should be ordered by timestamp
        result.Value[1].ShouldBe(assistantMessage);
    }

    [Fact]
    public async Task GetChatHistoryAsync_ShouldReturnEmptyList_WhenNoExperienceExists()
    {
        // Arrange
        var chatId = Guid.NewGuid();

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>()));

        // Act
        var result = await _chatService.GetChatHistoryAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChatHistoryAsync_ShouldReturnFailure_WhenRelationshipQueryFails()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var errorMessage = "Database error";

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Fail<IEnumerable<GraphRelationship>>(errorMessage));

        // Act
        var result = await _chatService.GetChatHistoryAsync(chatId);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("Failed to find experience for chat"));
    }

    [Fact]
    public async Task GetAllChatExperiencesAsync_ShouldReturnOrderedExperiences_WhenExperiencesExist()
    {
        // Arrange
        var experience1Id = Guid.NewGuid();
        var experience2Id = Guid.NewGuid();
        var chatId1 = Guid.NewGuid();
        var chatId2 = Guid.NewGuid();

        var experience1 = CreateTestMemorygram(experience1Id, MemorygramType.Experience, "Experience 1", chatId1.ToString(), createdAt: DateTimeOffset.UtcNow);
        var experience2 = CreateTestMemorygram(experience2Id, MemorygramType.Experience, "Experience 2", chatId2.ToString(), createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var relationships = new List<GraphRelationship>
        {
            new GraphRelationship(Guid.NewGuid(), experience1Id, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new GraphRelationship(Guid.NewGuid(), experience2Id, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        _mockMemorygramService.GetMemorygramByIdAsync(experience1Id)
            .Returns(Result.Ok(experience1));

        _mockMemorygramService.GetMemorygramByIdAsync(experience2Id)
            .Returns(Result.Ok(experience2));

        // Act
        var result = await _chatService.GetAllChatExperiencesAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].ShouldBe(experience1); // Should be ordered by CreatedAt descending
        result.Value[1].ShouldBe(experience2);
    }

    [Fact]
    public async Task GetAllChatExperiencesAsync_ShouldReturnEmptyList_WhenNoExperiencesExist()
    {
        // Arrange
        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>()));

        // Act
        var result = await _chatService.GetAllChatExperiencesAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllChatExperiencesAsync_ShouldReturnFailure_WhenRelationshipQueryFails()
    {
        // Arrange
        var errorMessage = "Database error";

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Fail<IEnumerable<GraphRelationship>>(errorMessage));

        // Act
        var result = await _chatService.GetAllChatExperiencesAsync();

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("Failed to get chat relationships"));
    }

    [Fact]
    public async Task GetExperienceForChatAsync_ShouldReturnExperience_WhenExperienceExists()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var experience = CreateTestMemorygram(experienceId, MemorygramType.Experience, "Test experience", chatId.ToString());

        var relationships = new List<GraphRelationship>
        {
            new GraphRelationship(Guid.NewGuid(), experienceId, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        _mockMemorygramService.GetMemorygramByIdAsync(experienceId)
            .Returns(Result.Ok(experience));

        // Act
        var result = await _chatService.GetExperienceForChatAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBe(experience);
    }

    [Fact]
    public async Task GetExperienceForChatAsync_ShouldReturnNull_WhenNoExperienceExists()
    {
        // Arrange
        var chatId = Guid.NewGuid();

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>()));

        // Act
        var result = await _chatService.GetExperienceForChatAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task GetExperienceForChatAsync_ShouldReturnFailure_WhenRelationshipQueryFails()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var errorMessage = "Database error";

        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Fail<IEnumerable<GraphRelationship>>(errorMessage));

        // Act
        var result = await _chatService.GetExperienceForChatAsync(chatId);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("Failed to get chat relationships"));
    }

    [Fact]
    public async Task GetChatHistoryAsync_ShouldFilterOnlyUserInputAndAssistantResponse()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var userMessageId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();
        var reflectionId = Guid.NewGuid();

        var experience = CreateTestMemorygram(experienceId, MemorygramType.Experience, "Test experience", chatId.ToString());
        var userMessage = CreateTestMemorygram(userMessageId, MemorygramType.UserInput, "Hello");
        var assistantMessage = CreateTestMemorygram(assistantMessageId, MemorygramType.AssistantResponse, "Hi there");
        var reflection = CreateTestMemorygram(reflectionId, MemorygramType.Reflection, "Reflection content");

        var relationships = new List<GraphRelationship>
        {
            new GraphRelationship(Guid.NewGuid(), experienceId, userMessageId, "ROOT_OF", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new GraphRelationship(Guid.NewGuid(), experienceId, assistantMessageId, "ROOT_OF", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new GraphRelationship(Guid.NewGuid(), experienceId, reflectionId, "ROOT_OF", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        // Mock GetExperienceForChatAsync
        _mockMemorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID")
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(new List<GraphRelationship>
            {
                new GraphRelationship(Guid.NewGuid(), experienceId, Guid.NewGuid(), "HAS_CHAT_ID", 1.0f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            }));

        _mockMemorygramService.GetMemorygramByIdAsync(experienceId)
            .Returns(Result.Ok(experience));

        // Mock GetChatHistoryAsync relationships
        _mockMemorygramService.GetRelationshipsByMemorygramIdAsync(experienceId, false, true)
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        _mockMemorygramService.GetMemorygramByIdAsync(userMessageId)
            .Returns(Result.Ok(userMessage));

        _mockMemorygramService.GetMemorygramByIdAsync(assistantMessageId)
            .Returns(Result.Ok(assistantMessage));

        _mockMemorygramService.GetMemorygramByIdAsync(reflectionId)
            .Returns(Result.Ok(reflection));

        // Act
        var result = await _chatService.GetChatHistoryAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2); // Should exclude Reflection
        result.Value.ShouldContain(userMessage);
        result.Value.ShouldContain(assistantMessage);
        result.Value.ShouldNotContain(reflection);
    }

    private static Memorygram CreateTestMemorygram(
        Guid id,
        MemorygramType type,
        string content,
        string? subtype = null,
        long timestamp = 0,
        DateTimeOffset? createdAt = null)
    {
        var created = createdAt ?? DateTimeOffset.UtcNow;
        return new Memorygram(
            Id: id,
            Content: content,
            Type: type,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: "Test",
            Timestamp: timestamp,
            CreatedAt: created,
            UpdatedAt: created,
            Subtype: subtype
        );
    }
}
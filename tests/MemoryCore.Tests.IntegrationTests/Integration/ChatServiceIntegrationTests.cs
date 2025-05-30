using MemoryCore.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace MemoryCore.Tests.IntegrationTests.Integration;

[Trait("Category", "Integration")]
[Collection("TestContainerCollection")]
public class ChatServiceIntegrationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _scopedServiceProvider;
    private readonly IChatService _chatService;
    private readonly IMemorygramService _memorygramService;
    private readonly Neo4jContainerFixture _neo4jFixture;

    public ChatServiceIntegrationTests(
        ITestOutputHelper output,
        Neo4jContainerFixture neo4jFixture)
    {
        _output = output;
        _neo4jFixture = neo4jFixture;

        _factory = new CustomWebApplicationFactory(neo4jFixture);
        
        _scope = _factory.Services.CreateScope();
        _scopedServiceProvider = _scope.ServiceProvider;
        
        // Get services from the factory's DI container
        _chatService = _scopedServiceProvider.GetRequiredService<IChatService>();
        _memorygramService = _scopedServiceProvider.GetRequiredService<IMemorygramService>();
    }

    [Fact]
    public async Task GetChatHistoryAsync_ShouldReturnOrderedHistory_WithRealData()
    {
        // Arrange
        var chatId = Guid.NewGuid();

        // Create test data
        var experienceId = Guid.NewGuid();
        var userMessageId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();
        var chatMetadataId = Guid.NewGuid();

        // Create Experience memorygram
        var experience = new Memorygram(
            Id: experienceId,
            Content: "New conversation started",
            Type: MemorygramType.Experience,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            Subtype: "Chat"
        );

        // Create UserInput memorygram
        var userMessage = new Memorygram(
            Id: userMessageId,
            Content: "Hello, how are you?",
            Type: MemorygramType.UserInput,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 100,
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-8),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-8)
        );

        // Create AssistantResponse memorygram
        var assistantMessage = new Memorygram(
            Id: assistantMessageId,
            Content: "I'm doing well, thank you for asking!",
            Type: MemorygramType.AssistantResponse,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 50,
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-7),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-7)
        );

        // Create chat metadata node (simulated as a memorygram for simplicity)
        var chatMetadata = new Memorygram(
            Id: chatMetadataId,
            Content: $"Chat metadata for {chatId}",
            Type: MemorygramType.UserInput, // Using UserInput as placeholder for metadata
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-10)
        );

        // Persist memorygrams
        await _memorygramService.CreateOrUpdateMemorygramAsync(experience);
        await _memorygramService.CreateOrUpdateMemorygramAsync(userMessage);
        await _memorygramService.CreateOrUpdateMemorygramAsync(assistantMessage);
        await _memorygramService.CreateOrUpdateMemorygramAsync(chatMetadata);

        // Create relationships
        await _memorygramService.CreateRelationshipAsync(experienceId, chatMetadataId, "HAS_CHAT_ID", 1.0f, $"{{\"chatId\":\"{chatId}\"}}");
        await _memorygramService.CreateRelationshipAsync(experienceId, userMessageId, "ROOT_OF", 1.0f);
        await _memorygramService.CreateRelationshipAsync(experienceId, assistantMessageId, "ROOT_OF", 1.0f);

        _output.WriteLine($"Created test data for chat {chatId}");

        // Act
        var result = await _chatService.GetChatHistoryAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        
        // Verify ordering by timestamp
        result.Value[0].Timestamp.ShouldBeLessThan(result.Value[1].Timestamp);
        result.Value[0].Type.ShouldBe(MemorygramType.UserInput);
        result.Value[1].Type.ShouldBe(MemorygramType.AssistantResponse);

        _output.WriteLine($"Retrieved {result.Value.Count} messages from chat history");
    }

    [Fact]
    public async Task GetAllChatExperiencesAsync_ShouldReturnAllExperiences_WithRealData()
    {
        // Arrange
        var chatId1 = Guid.NewGuid();
        var chatId2 = Guid.NewGuid();
        var experienceId1 = Guid.NewGuid();
        var experienceId2 = Guid.NewGuid();
        var chatMetadataId1 = Guid.NewGuid();
        var chatMetadataId2 = Guid.NewGuid();

        // Create Experience memorygrams
        var experience1 = new Memorygram(
            Id: experienceId1,
            Content: "First conversation",
            Type: MemorygramType.Experience,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-20),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-20),
            Subtype: "Chat"
        );

        var experience2 = new Memorygram(
            Id: experienceId2,
            Content: "Second conversation",
            Type: MemorygramType.Experience,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            Subtype: "Chat"
        );

        // Create chat metadata nodes
        var chatMetadata1 = new Memorygram(
            Id: chatMetadataId1,
            Content: $"Chat metadata for {chatId1}",
            Type: MemorygramType.UserInput,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-20),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );

        var chatMetadata2 = new Memorygram(
            Id: chatMetadataId2,
            Content: $"Chat metadata for {chatId2}",
            Type: MemorygramType.UserInput,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-10)
        );

        // Persist memorygrams
        await _memorygramService.CreateOrUpdateMemorygramAsync(experience1);
        await _memorygramService.CreateOrUpdateMemorygramAsync(experience2);
        await _memorygramService.CreateOrUpdateMemorygramAsync(chatMetadata1);
        await _memorygramService.CreateOrUpdateMemorygramAsync(chatMetadata2);

        // Create relationships
        await _memorygramService.CreateRelationshipAsync(experienceId1, chatMetadataId1, "HAS_CHAT_ID", 1.0f, $"{{\"chatId\":\"{chatId1}\"}}");
        await _memorygramService.CreateRelationshipAsync(experienceId2, chatMetadataId2, "HAS_CHAT_ID", 1.0f, $"{{\"chatId\":\"{chatId2}\"}}");

        _output.WriteLine($"Created test data for chats {chatId1} and {chatId2}");

        // Act
        var result = await _chatService.GetAllChatExperiencesAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(2);
        
        // Should contain our test experiences
        var testExperiences = result.Value.Where(e => 
            e.Id == experienceId1 || e.Id == experienceId2).ToList();
        testExperiences.Count.ShouldBe(2);

        // Verify ordering (most recent first)
        var orderedExperiences = result.Value.OrderByDescending(e => e.CreatedAt).ToList();
        result.Value.ShouldBe(orderedExperiences);

        _output.WriteLine($"Retrieved {result.Value.Count} chat experiences");
    }

    [Fact]
    public async Task GetExperienceForChatAsync_ShouldReturnCorrectExperience_WithRealData()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var chatMetadataId = Guid.NewGuid();

        // Create Experience memorygram
        var experience = new Memorygram(
            Id: experienceId,
            Content: "Test conversation experience",
            Type: MemorygramType.Experience,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            Subtype: "Chat"
        );

        // Create chat metadata node
        var chatMetadata = new Memorygram(
            Id: chatMetadataId,
            Content: $"Chat metadata for {chatId}",
            Type: MemorygramType.UserInput,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-5)
        );

        // Persist memorygrams
        await _memorygramService.CreateOrUpdateMemorygramAsync(experience);
        await _memorygramService.CreateOrUpdateMemorygramAsync(chatMetadata);

        // Create relationship
        await _memorygramService.CreateRelationshipAsync(experienceId, chatMetadataId, "HAS_CHAT_ID", 1.0f, $"{{\"chatId\":\"{chatId}\"}}");

        _output.WriteLine($"Created test experience for chat {chatId}");

        // Act
        var result = await _chatService.GetExperienceForChatAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(experienceId);
        result.Value.Type.ShouldBe(MemorygramType.Experience);
        result.Value.Subtype.ShouldBe("Chat");

        _output.WriteLine($"Retrieved experience {result.Value.Id} for chat {chatId}");
    }

    [Fact]
    public async Task GetExperienceForChatAsync_ShouldReturnNull_WhenNoExperienceExists()
    {
        // Arrange
        var nonExistentChatId = Guid.NewGuid();

        // Act
        var result = await _chatService.GetExperienceForChatAsync(nonExistentChatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();

        _output.WriteLine($"Correctly returned null for non-existent chat {nonExistentChatId}");
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
        var chatMetadataId = Guid.NewGuid();

        var experience = new Memorygram(
            Id: experienceId,
            Content: "Test experience",
            Type: MemorygramType.Experience,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            Subtype: "Chat"
        );

        var userMessage = new Memorygram(
            Id: userMessageId,
            Content: "Hello",
            Type: MemorygramType.UserInput,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: 1000,
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-8),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-8)
        );

        var assistantMessage = new Memorygram(
            Id: assistantMessageId,
            Content: "Hi there",
            Type: MemorygramType.AssistantResponse,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: 2000,
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-7),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-7)
        );

        var reflection = new Memorygram(
            Id: reflectionId,
            Content: "Reflection content",
            Type: MemorygramType.Reflection,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: 3000,
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-6),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-6)
        );

        var chatMetadata = new Memorygram(
            Id: chatMetadataId,
            Content: $"Chat metadata for {chatId}",
            Type: MemorygramType.UserInput,
            TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
            ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
            ContextEmbedding: new float[] { 0.7f, 0.8f, 0.9f },
            MetadataEmbedding: new float[] { 1.0f, 1.1f, 1.2f },
            Source: "ChatServiceIntegrationTest",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-10)
        );

        // Persist memorygrams
        await _memorygramService.CreateOrUpdateMemorygramAsync(experience);
        await _memorygramService.CreateOrUpdateMemorygramAsync(userMessage);
        await _memorygramService.CreateOrUpdateMemorygramAsync(assistantMessage);
        await _memorygramService.CreateOrUpdateMemorygramAsync(reflection);
        await _memorygramService.CreateOrUpdateMemorygramAsync(chatMetadata);

        // Create relationships
        await _memorygramService.CreateRelationshipAsync(experienceId, chatMetadataId, "HAS_CHAT_ID", 1.0f, $"{{\"chatId\":\"{chatId}\"}}");
        await _memorygramService.CreateRelationshipAsync(experienceId, userMessageId, "ROOT_OF", 1.0f);
        await _memorygramService.CreateRelationshipAsync(experienceId, assistantMessageId, "ROOT_OF", 1.0f);
        await _memorygramService.CreateRelationshipAsync(experienceId, reflectionId, "ROOT_OF", 1.0f);

        _output.WriteLine($"Created test data with reflection for chat {chatId}");

        // Act
        var result = await _chatService.GetChatHistoryAsync(chatId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2); // Should exclude Reflection
        result.Value.ShouldContain(m => m.Id == userMessageId);
        result.Value.ShouldContain(m => m.Id == assistantMessageId);
        result.Value.ShouldNotContain(m => m.Id == reflectionId);

        _output.WriteLine($"Retrieved {result.Value.Count} messages, correctly filtered out reflection");
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _factory?.Dispose();
    }
}

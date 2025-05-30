using MemoryCore.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using Neo4j.Driver;
using NSubstitute;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MemoryCore.Tests.IntegrationTests.Integration;

[Trait("Category", "Integration")]
[Collection("TestContainerCollection")]
public class ExperienceCreationIntegrationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _scopedServiceProvider;
    private readonly IResponderService _responderService;
    private readonly IMemoryQueryService _memoryQueryService;
    private readonly IMemorygramRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly Neo4jContainerFixture _neo4jFixture;

    public ExperienceCreationIntegrationTests(
        ITestOutputHelper output,
        Neo4jContainerFixture neo4jFixture)
    {
        _output = output;
        _neo4jFixture = neo4jFixture;

        _factory = new CustomWebApplicationFactory(neo4jFixture);
        
        _scope = _factory.Services.CreateScope();
        _scopedServiceProvider = _scope.ServiceProvider;
        
        // Pipeline files are automatically copied by CustomWebApplicationFactory
        
        // Get services from the factory's DI container
        _memoryQueryService = _scopedServiceProvider.GetRequiredService<IMemoryQueryService>();
        _repository = _scopedServiceProvider.GetRequiredService<IMemorygramRepository>();
        _embeddingService = _scopedServiceProvider.GetRequiredService<IEmbeddingService>();
        
        // Create ResponderService manually with mock dependencies for integration testing
        var pipelineExecutorService = CreateMockPipelineExecutorService();
        var pipelinesRepository = _scopedServiceProvider.GetRequiredService<IPipelinesRepository>();
        var promptConstructor = CreateMockPromptConstructor();
        var languageModelService = CreateMockLanguageModelService();
        var reflectiveResponder = CreateMockReflectiveResponder();
        var memorygramService = _scopedServiceProvider.GetRequiredService<IMemorygramService>();
        var logger = _scopedServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResponderService>>();
        
        _responderService = new ResponderService(
            pipelineExecutorService,
            pipelinesRepository,
            promptConstructor,
            languageModelService,
            reflectiveResponder,
            _memoryQueryService,
            memorygramService,
            logger
        );
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task ExperienceCreationWorkflow_FirstMessageInChat_ShouldCreateExperience()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var chatId = Guid.NewGuid().ToString();
        var userInput = "Hello, this is my first message in this chat";
        var request = new PipelineExecutionRequest
        {
            UserInput = userInput,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId } }
        };

        // Act
        var result = await _responderService.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Give Neo4j a moment to persist the data
        await Task.Delay(200);
        
        // Verify that chat history contains user input, assistant response, and experience
        var chatHistory = await _memoryQueryService.GetChatHistoryAsync(chatId);
        chatHistory.IsSuccess.ShouldBeTrue();
        
        var memorygrams = chatHistory.Value.ToList();
        memorygrams.Count.ShouldBeGreaterThanOrEqualTo(3); // UserInput, AssistantResponse, Experience
        
        // Check for user input memorygram
        var userInputMemorygram = memorygrams.FirstOrDefault(m => m.Type == MemorygramType.UserInput);
        userInputMemorygram.ShouldNotBeNull();
        userInputMemorygram.Content.ShouldBe(userInput);
        userInputMemorygram.Subtype.ShouldBe("Chat");
        
        // Check for assistant response memorygram
        var assistantResponseMemorygram = memorygrams.FirstOrDefault(m => m.Type == MemorygramType.AssistantResponse);
        assistantResponseMemorygram.ShouldNotBeNull();
        assistantResponseMemorygram.Subtype.ShouldBe("Chat");
        
        // Check for experience memorygram
        var experienceMemorygram = memorygrams.FirstOrDefault(m => m.Type == MemorygramType.Experience);
        experienceMemorygram.ShouldNotBeNull();
        experienceMemorygram.Content.ShouldContain(userInput);
        experienceMemorygram.Content.ShouldContain("New conversation started with:");
        experienceMemorygram.Subtype.ShouldBe("Chat");
        
        _output.WriteLine($"Created experience: {experienceMemorygram.Content}");
    }

    [Fact]
    public async Task ExperienceCreationWorkflow_SecondMessageInChat_ShouldUpdateExperience()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var chatId = Guid.NewGuid().ToString();
        var firstUserInput = "Hello, this is my first message";
        var secondUserInput = "This is my follow-up message";

        var firstRequest = new PipelineExecutionRequest
        {
            UserInput = firstUserInput,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId } }
        };

        var secondRequest = new PipelineExecutionRequest
        {
            UserInput = secondUserInput,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId } }
        };

        // Act
        // First message - should create experience
        var firstResult = await _responderService.ProcessRequestAsync(firstRequest);
        firstResult.IsSuccess.ShouldBeTrue();

        // Give Neo4j a moment to persist the data
        await Task.Delay(200);

        // Second message - should update experience
        var secondResult = await _responderService.ProcessRequestAsync(secondRequest);
        secondResult.IsSuccess.ShouldBeTrue();

        // Give Neo4j a moment to persist the data
        await Task.Delay(200);

        // Assert
        var chatHistory = await _memoryQueryService.GetChatHistoryAsync(chatId);
        chatHistory.IsSuccess.ShouldBeTrue();
        
        var memorygrams = chatHistory.Value.ToList();
        
        // Should have: 2 UserInputs, 2 AssistantResponses, and 1 updated Experience
        var userInputs = memorygrams.Where(m => m.Type == MemorygramType.UserInput).ToList();
        var assistantResponses = memorygrams.Where(m => m.Type == MemorygramType.AssistantResponse).ToList();
        var experiences = memorygrams.Where(m => m.Type == MemorygramType.Experience).ToList();
        
        userInputs.Count.ShouldBe(2);
        assistantResponses.Count.ShouldBe(2);
        experiences.Count.ShouldBe(1); // Should be updated, not duplicated
        
        // Check that experience contains both messages
        var experience = experiences.First();
        experience.Content.ShouldContain(firstUserInput);
        experience.Content.ShouldContain(secondUserInput);
        experience.Content.ShouldContain("New conversation started with:");
        experience.Content.ShouldContain("Continued with:");
        experience.Subtype.ShouldBe("Chat");
        
        _output.WriteLine($"Updated experience: {experience.Content}");
    }

    [Fact]
    public async Task ExperienceCreationWorkflow_MultipleChats_ShouldCreateSeparateExperiences()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var chatId1 = Guid.NewGuid().ToString();
        var chatId2 = Guid.NewGuid().ToString();
        var userInput1 = "Hello from chat 1";
        var userInput2 = "Hello from chat 2";

        var request1 = new PipelineExecutionRequest
        {
            UserInput = userInput1,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId1 } }
        };

        var request2 = new PipelineExecutionRequest
        {
            UserInput = userInput2,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId2 } }
        };

        // Act
        var result1 = await _responderService.ProcessRequestAsync(request1);
        var result2 = await _responderService.ProcessRequestAsync(request2);

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        
        // Give Neo4j a moment to persist the data
        await Task.Delay(200);
        
        // Check chat 1 history
        var chatHistory1 = await _memoryQueryService.GetChatHistoryAsync(chatId1);
        chatHistory1.IsSuccess.ShouldBeTrue();
        var experience1 = chatHistory1.Value.FirstOrDefault(m => m.Type == MemorygramType.Experience);
        experience1.ShouldNotBeNull();
        experience1.Content.ShouldContain(userInput1);
        experience1.Subtype.ShouldBe("Chat");
        
        // Check chat 2 history
        var chatHistory2 = await _memoryQueryService.GetChatHistoryAsync(chatId2);
        chatHistory2.IsSuccess.ShouldBeTrue();
        var experience2 = chatHistory2.Value.FirstOrDefault(m => m.Type == MemorygramType.Experience);
        experience2.ShouldNotBeNull();
        experience2.Content.ShouldContain(userInput2);
        experience2.Subtype.ShouldBe("Chat");
        
        // Experiences should be different
        experience1.Id.ShouldNotBe(experience2.Id);
        experience1.Content.ShouldNotBe(experience2.Content);
        
        _output.WriteLine($"Chat 1 experience: {experience1.Content}");
        _output.WriteLine($"Chat 2 experience: {experience2.Content}");
    }

    [Fact]
    public async Task ExperienceCreationWorkflow_WithoutChatId_ShouldNotCreateExperience()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var userInput = "Hello without chat ID";
        var request = new PipelineExecutionRequest
        {
            UserInput = userInput,
            SessionMetadata = new Dictionary<string, object> { { "userId", "test-user" } }
        };

        // Act
        var result = await _responderService.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Give Neo4j a moment to persist the data
        await Task.Delay(200);
        
        // Since there's no chatId, we can't query chat history directly,
        // but we can verify that no experience memorygram was created by checking all memorygrams
        using var session = _scopedServiceProvider.GetRequiredService<IDriver>().AsyncSession();
        var experienceCount = await session.ExecuteReadAsync(async tx =>
        {
            var result = await tx.RunAsync("MATCH (m:Memorygram {type: $type}) RETURN count(m) as count", 
                new { type = MemorygramType.Experience.ToString() });
            var record = await result.SingleAsync();
            return record["count"].As<int>();
        });
        
        experienceCount.ShouldBe(0);
        _output.WriteLine("Request processed without chat ID - no experience created as expected");
    }


    private IPipelineExecutorService CreateMockPipelineExecutorService()
    {
        var mock = Substitute.For<IPipelineExecutorService>();
        mock.ExecutePipelineAsync(Arg.Any<PipelineExecutionState>())
            .Returns(args => Task.FromResult(FluentResults.Result.Ok((PipelineExecutionState)args[0])));
        return mock;
    }

    private IPromptConstructor CreateMockPromptConstructor()
    {
        var mock = Substitute.For<IPromptConstructor>();
        mock.ConstructPrompt(Arg.Any<PipelineExecutionState>())
            .Returns(FluentResults.Result.Ok(new PromptConstructionResult
            {
                Request = new ChatCompletionRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "user", Content = "test" }
                    }
                },
                SystemPrompt = "Test system prompt"
            }));
        return mock;
    }

    private ILanguageModelService CreateMockLanguageModelService()
    {
        var mock = Substitute.For<ILanguageModelService>();
        mock.GenerateCompletionAsync(Arg.Any<ChatCompletionRequest>(), Arg.Any<LanguageModelType>())
            .Returns(args =>
            {
                var request = (ChatCompletionRequest)args[0];
                var userMessage = request.Messages.LastOrDefault()?.Content ?? "test";
                var response = $"Mock response to: {userMessage}";
                return Task.FromResult(FluentResults.Result.Ok(response));
            });
        return mock;
    }

    private IReflectiveResponder CreateMockReflectiveResponder()
    {
        var mock = Substitute.For<IReflectiveResponder>();
        mock.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(FluentResults.Result.Ok(new ResponseEvaluation
            {
                ShouldDispatch = true,
                EvaluationNotes = "Test evaluation",
                Confidence = 1.0f
            })));
        return mock;
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

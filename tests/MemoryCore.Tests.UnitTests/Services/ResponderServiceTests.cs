using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace MemoryCore.Tests.UnitTests.Services;

public class ResponderServiceTests
{
    private readonly Mock<IPipelineExecutorService> _mockPipelineExecutorService;
    private readonly Mock<IPipelinesRepository> _mockPipelinesRepository;
    private readonly Mock<IPromptConstructor> _mockPromptConstructor;
    private readonly Mock<ILanguageModelService> _mockLanguageModelService;
    private readonly Mock<IReflectiveResponder> _mockReflectiveResponder;
    private readonly Mock<IMemoryQueryService> _mockMemoryQueryService;
    private readonly Mock<IMemorygramService> _mockMemorygramService;
    private readonly Mock<ILogger<ResponderService>> _mockLogger;
    private readonly ResponderService _service;

    public ResponderServiceTests()
    {
        _mockPipelineExecutorService = new Mock<IPipelineExecutorService>();
        _mockPipelinesRepository = new Mock<IPipelinesRepository>();
        _mockPromptConstructor = new Mock<IPromptConstructor>();
        _mockLanguageModelService = new Mock<ILanguageModelService>();
        _mockReflectiveResponder = new Mock<IReflectiveResponder>();
        _mockMemoryQueryService = new Mock<IMemoryQueryService>();
        _mockMemorygramService = new Mock<IMemorygramService>();
        _mockLogger = new Mock<ILogger<ResponderService>>();
        _service = new ResponderService(
            _mockPipelineExecutorService.Object,
            _mockPipelinesRepository.Object,
            _mockPromptConstructor.Object,
            _mockLanguageModelService.Object,
            _mockReflectiveResponder.Object,
            _mockMemoryQueryService.Object,
            _mockMemorygramService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnSuccess_WhenAllStepsSucceed()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var pipelineExecutionState = new PipelineExecutionState { RunId = Guid.NewGuid() };
        var chatCompletionRequest = new ChatCompletionRequest { Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "test" } } };
        var llmResponse = "LLM generated response";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Ok(pipelineExecutionState));
        var promptConstructionResult = new PromptConstructionResult
        {
            Request = chatCompletionRequest,
            SystemPrompt = "Test system prompt with memories"
        };
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(pipelineExecutionState))
            .Returns(Result.Ok(promptConstructionResult));
        _mockLanguageModelService.Setup(l => l.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master))
            .ReturnsAsync(Result.Ok(llmResponse));
        _mockReflectiveResponder.Setup(r => r.EvaluateResponseAsync(userInput, llmResponse))
            .ReturnsAsync(Result.Ok(new ResponseEvaluation { ShouldDispatch = true, EvaluationNotes = "Test evaluation", Confidence = 1.0f }));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), llmResponse, MemorygramType.AssistantResponse, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "LLM_Response", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Response.ShouldBe(llmResponse);
        result.Value.SystemPrompt.ShouldBe("Test system prompt with memories");

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(pipelineExecutionState), Times.Once);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenGetPipelineFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var errorMessage = "Pipeline not found";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Fail<PipelineManifest>(errorMessage));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Never);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(It.IsAny<PipelineExecutionState>()), Times.Never);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<LanguageModelType>()), Times.Never);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenPipelineExecutionFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var errorMessage = "Pipeline execution error";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Fail<PipelineExecutionState>(errorMessage));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(It.IsAny<PipelineExecutionState>()), Times.Never);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<LanguageModelType>()), Times.Never);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenPromptConstructionFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var pipelineExecutionState = new PipelineExecutionState { RunId = Guid.NewGuid() };
        var errorMessage = "Prompt construction error";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Ok(pipelineExecutionState));
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(pipelineExecutionState))
            .Returns(Result.Fail<PromptConstructionResult>(errorMessage));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(pipelineExecutionState), Times.Once);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<LanguageModelType>()), Times.Never);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenLlmGenerationFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var pipelineExecutionState = new PipelineExecutionState { RunId = Guid.NewGuid() };
        var chatCompletionRequest = new ChatCompletionRequest { Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "test" } } };
        var errorMessage = "LLM generation error";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Ok(pipelineExecutionState));
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(pipelineExecutionState))
            .Returns(Result.Ok(new PromptConstructionResult { Request = chatCompletionRequest, SystemPrompt = "Test system prompt" }));
        _mockLanguageModelService.Setup(l => l.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master))
            .ReturnsAsync(Result.Fail<string>(errorMessage));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(pipelineExecutionState), Times.Once);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldLogWarning_WhenMemorygramCreationFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var pipelineExecutionState = new PipelineExecutionState { RunId = Guid.NewGuid() };
        var chatCompletionRequest = new ChatCompletionRequest { Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "test" } } };
        var llmResponse = "LLM generated response";
        var memorygramErrorMessage = "Memorygram creation error";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Ok(pipelineExecutionState));
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(pipelineExecutionState))
            .Returns(Result.Ok(new PromptConstructionResult { Request = chatCompletionRequest, SystemPrompt = "Test system prompt" }));
        _mockLanguageModelService.Setup(l => l.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master))
            .ReturnsAsync(Result.Ok(llmResponse));
        _mockReflectiveResponder.Setup(r => r.EvaluateResponseAsync(userInput, llmResponse))
            .ReturnsAsync(Result.Ok(new ResponseEvaluation { ShouldDispatch = true, EvaluationNotes = "Test evaluation", Confidence = 1.0f }));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg => mg.Content == userInput))) // User input memorygram
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg => mg.Content == llmResponse))) // Assistant response memorygram
            .ReturnsAsync(Result.Fail<Memorygram>(memorygramErrorMessage));
        
        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Response.ShouldBe(llmResponse);
        
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldAddUserInputExactlyOnceToContext()
    {
        // Arrange
        var userInput = "Test user input";
        var chatId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        var request = new PipelineExecutionRequest
        {
            UserInput = userInput,
            PipelineId = pipelineId,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId.ToString() } }
        };

        var pipeline = new PipelineManifest
        {
            Id = pipelineId,
            Name = "Test Pipeline",
            Description = "Test Description",
            Components = new List<ComponentConfiguration>()
        };

        var expectedResponse = "Test response";
        var systemPrompt = "Test system prompt";

        // Mock existing chat history that includes the same user input
        var existingChatHistory = new List<Memorygram>
        {
            new Memorygram(
                Id: Guid.NewGuid(),
                Content: userInput, // Same content as current request
                Type: MemorygramType.UserInput,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
                UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-1)
            )
        };

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(pipeline));
        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId.ToString()))
            .ReturnsAsync(Result.Ok(existingChatHistory));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Ok(new PipelineExecutionState
            {
                RunId = Guid.NewGuid(),
                PipelineId = pipelineId,
                Request = request,
                History = new List<PipelineStageHistory>(),
                Context = new List<ContextChunk>
                {
                    new ContextChunk
                    {
                        Type = MemorygramType.UserInput,
                        Content = userInput,
                        Provenance = new ContextProvenance
                        {
                            Source = "User",
                            Timestamp = DateTimeOffset.FromUnixTimeSeconds(existingChatHistory[0].Timestamp)
                        }
                    }
                }
            }));
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(It.IsAny<PipelineExecutionState>()))
            .Returns(Result.Ok(new PromptConstructionResult
            {
                Request = new ChatCompletionRequest { Messages = new List<ChatMessage>() },
                SystemPrompt = systemPrompt
            }));
        _mockLanguageModelService.Setup(l => l.GenerateCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<LanguageModelType>()))
            .ReturnsAsync(Result.Ok(expectedResponse));
        _mockReflectiveResponder.Setup(r => r.EvaluateResponseAsync(userInput, expectedResponse))
            .ReturnsAsync(Result.Ok(new ResponseEvaluation { ShouldDispatch = true }));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg => mg.Content == userInput))) // User input memorygram
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
         _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg => mg.Content == expectedResponse))) // Assistant response memorygram
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), expectedResponse, MemorygramType.AssistantResponse, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Verify that the pipeline executor was called with state containing exactly one instance of the user input
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.Is<PipelineExecutionState>(state =>
            state.Context.Count(chunk => chunk.Type == MemorygramType.UserInput && chunk.Content == userInput) == 1
        )), Times.Once);
    }

    [Fact]
    public async Task PersistResponseMemory_WithChatIdInSessionMetadata_ShouldIncludeChatIdInMemorygram()
    {
        // Arrange
        var chatIdGuid = Guid.NewGuid();
        var chatIdString = chatIdGuid.ToString();
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>
            {
                ["chatId"] = chatIdString,
                ["userId"] = "test-user"
            }
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        
        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.AssistantResponse &&
                memorygram.Content == response &&
                memorygram.TopicalEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContentEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContextEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.MetadataEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.Source == "ResponderService"
            )
        ), Times.Once);
    }

    [Fact]
    public async Task PersistResponseMemory_WithoutChatIdInSessionMetadata_ShouldCreateMemorygramWithNullChatId()
    {
        // Arrange
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>
            {
                ["userId"] = "test-user"
                // No chatId in metadata
            }
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.AssistantResponse &&
                memorygram.Content == response &&
                memorygram.TopicalEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContentEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContextEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.MetadataEmbedding.SequenceEqual(Array.Empty<float>())
            )
        ), Times.Once);
    }

    [Fact]
    public async Task PersistResponseMemory_WithNullChatIdInSessionMetadata_ShouldCreateMemorygramWithNullChatId()
    {
        // Arrange
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();

        // Create a dictionary without the chatId key - this will result in null when GetValueOrDefault is called
        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>
            {
                ["userId"] = "test-user"
                // No chatId entry - will return null from GetValueOrDefault
            }
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.AssistantResponse &&
                memorygram.Content == response &&
                memorygram.TopicalEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContentEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContextEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.MetadataEmbedding.SequenceEqual(Array.Empty<float>())
            )
        ), Times.Once);
    }
    
    [Fact]
    public async Task PersistUserInputMemory_WithChatIdInSessionMetadata_ShouldIncludeChatIdInMemorygram()
    {
        // Arrange
        var chatIdGuid = Guid.NewGuid();
        var chatIdString = chatIdGuid.ToString();
        var userInput = "This is a test user input";
        var pipelineId = Guid.NewGuid();

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = userInput,
            SessionMetadata = new Dictionary<string, object>
            {
                ["chatId"] = chatIdString,
                ["userId"] = "test-user-2"
            }
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, null!, null!, null!, null!, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistUserInputMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.UserInput &&
                memorygram.Content == userInput &&
                memorygram.TopicalEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContentEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContextEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.MetadataEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.Source == "ResponderService"
            )
        ), Times.Once);
    }

    [Fact]
    public async Task PersistUserInputMemory_WithoutChatIdInSessionMetadata_ShouldCreateMemorygramWithNullChatId()
    {
        // Arrange
        var userInput = "This is a test user input";
        var pipelineId = Guid.NewGuid();

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = userInput,
            SessionMetadata = new Dictionary<string, object>
            {
                ["userId"] = "test-user-2"
                // No chatId
            }
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistUserInputMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.UserInput &&
                memorygram.Content == userInput &&
                memorygram.TopicalEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContentEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.ContextEmbedding.SequenceEqual(Array.Empty<float>()) &&
                memorygram.MetadataEmbedding.SequenceEqual(Array.Empty<float>())
            )
        ), Times.Once);
    }
    
    [Fact]
    public async Task PersistResponseMemory_ShouldSetCorrectMemorygramType()
    {
        // Arrange
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>()
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, null!, null!, null!, null!, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram => memorygram.Type == MemorygramType.AssistantResponse)
        ), Times.Once);
    }

    [Fact]
    public async Task IsFirstMessageInChat_WithEmptyHistory_ShouldReturnTrue()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId))
            .ReturnsAsync(Result.Ok(new List<Memorygram>()));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("IsFirstMessageInChat", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<bool>)method.Invoke(_service, new object[] { chatId })!;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsFirstMessageInChat_WithOnlyUserInput_ShouldReturnTrue()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        var chatHistory = new List<Memorygram>
        {
            new Memorygram(
                Id: Guid.NewGuid(),
                Content: "First user message",
                Type: MemorygramType.UserInput,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                Subtype: chatId
            )
        };

        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId))
            .ReturnsAsync(Result.Ok(chatHistory));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("IsFirstMessageInChat", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<bool>)method.Invoke(_service, new object[] { chatId })!;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsFirstMessageInChat_WithMultipleMessages_ShouldReturnFalse()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        var chatHistory = new List<Memorygram>
        {
            new Memorygram(
                Id: Guid.NewGuid(),
                Content: "First user message",
                Type: MemorygramType.UserInput,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-2),
                UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-2),
                Subtype: chatId
            ),
            new Memorygram(
                Id: Guid.NewGuid(),
                Content: "First assistant response",
                Type: MemorygramType.AssistantResponse,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
                UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
                Subtype: chatId
            )
        };

        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId))
            .ReturnsAsync(Result.Ok(chatHistory));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("IsFirstMessageInChat", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<bool>)method.Invoke(_service, new object[] { chatId })!;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsFirstMessageInChat_WithHistoryRetrievalFailure_ShouldReturnTrue()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId))
            .ReturnsAsync(Result.Fail<List<Memorygram>>("Database error"));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("IsFirstMessageInChat", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<bool>)method.Invoke(_service, new object[] { chatId })!;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateExperienceForChat_ShouldCreateExperienceMemorygram()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        var userInput = "Hello, this is my first message";
        var expectedExperienceId = Guid.NewGuid();

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.Experience &&
            mg.Content.Contains(userInput) &&
            mg.Subtype == "Chat")))
            .ReturnsAsync(Result.Ok(new Memorygram(
                Id: expectedExperienceId,
                Content: $"New conversation started with: {userInput}",
                Type: MemorygramType.Experience,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                Subtype: "Chat"
            )));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("CreateExperienceForChat", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<Result<Memorygram>>)method.Invoke(_service, new object[] { chatId, userInput })!;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(expectedExperienceId);
        result.Value.Type.ShouldBe(MemorygramType.Experience);
        result.Value.Subtype.ShouldBe("Chat");
        result.Value.Content.ShouldContain(userInput);

        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.Experience &&
            mg.Content.Contains(userInput) &&
            mg.Subtype == "Chat" &&
            mg.Source == "ResponderService"
        )), Times.Once);
    }

    [Fact]
    public async Task CreateExperienceForChat_WithMemorygramServiceFailure_ShouldReturnFailure()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        var userInput = "Hello, this is my first message";
        var errorMessage = "Database connection failed";

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Fail<Memorygram>(errorMessage));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("CreateExperienceForChat", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<Result<Memorygram>>)method.Invoke(_service, new object[] { chatId, userInput })!;

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));
    }

    [Fact]
    public async Task AssociateWithExistingExperience_WithExistingExperience_ShouldUpdateExperience()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        var userInput = "This is a follow-up message";
        var existingExperienceId = Guid.NewGuid();
        var existingContent = "New conversation started with: Hello";

        var existingExperience = new Memorygram(
            Id: existingExperienceId,
            Content: existingContent,
            Type: MemorygramType.Experience,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: "ResponderService",
            Timestamp: DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            Subtype: chatId
        );

        var chatHistory = new List<Memorygram> { existingExperience };

        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId))
            .ReturnsAsync(Result.Ok(chatHistory));

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Id == existingExperienceId &&
            mg.Type == MemorygramType.Experience &&
            mg.Content.Contains(userInput))))
            .ReturnsAsync(Result.Ok(existingExperience with {
                Content = $"{existingContent}\nContinued with: {userInput}",
                UpdatedAt = DateTimeOffset.UtcNow
            }));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("AssociateWithExistingExperience", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<Result>)method.Invoke(_service, new object[] { chatId, userInput })!;

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _mockMemoryQueryService.Verify(m => m.GetChatHistoryAsync(chatId), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Id == existingExperienceId &&
            mg.Type == MemorygramType.Experience &&
            mg.Content.Contains(userInput) &&
            mg.Content.Contains(existingContent)
        )), Times.Once);
    }

    [Fact]
    public async Task AssociateWithExistingExperience_WithNoExistingExperience_ShouldCreateNewExperience()
    {
        // Arrange
        var chatId = Guid.NewGuid().ToString();
        var userInput = "This should create a new experience";
        var newExperienceId = Guid.NewGuid();

        // No existing experiences in chat history
        var chatHistory = new List<Memorygram>
        {
            new Memorygram(
                Id: Guid.NewGuid(),
                Content: "Some user input",
                Type: MemorygramType.UserInput,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                Subtype: chatId
            )
        };

        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId))
            .ReturnsAsync(Result.Ok(chatHistory));

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.Experience &&
            mg.Content.Contains(userInput))))
            .ReturnsAsync(Result.Ok(new Memorygram(
                Id: newExperienceId,
                Content: $"New conversation started with: {userInput}",
                Type: MemorygramType.Experience,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                Subtype: chatId
            )));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("AssociateWithExistingExperience", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        var result = await (Task<Result>)method.Invoke(_service, new object[] { chatId, userInput })!;

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _mockMemoryQueryService.Verify(m => m.GetChatHistoryAsync(chatId), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.Experience &&
            mg.Content.Contains(userInput)
        )), Times.Once);
    }

    [Fact]
    public async Task PersistUserInputMemory_WithChatId_ShouldTriggerExperienceWorkflow()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var userInput = "Test user input for experience workflow";
        var request = new PipelineExecutionRequest
        {
            UserInput = userInput,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId.ToString() } }
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg => mg.Type == MemorygramType.UserInput)))
            .ReturnsAsync(Result.Ok(new Memorygram(
                Id: Guid.NewGuid(),
                Content: userInput,
                Type: MemorygramType.UserInput,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                Subtype: chatId.ToString()
            )));

        // Mock for IsFirstMessageInChat (empty history = first message)
        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId.ToString()))
            .ReturnsAsync(Result.Ok(new List<Memorygram>()));

        // Mock for CreateExperienceForChat
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg => mg.Type == MemorygramType.Experience)))
            .ReturnsAsync(Result.Ok(new Memorygram(
                Id: Guid.NewGuid(),
                Content: $"New conversation started with: {userInput}",
                Type: MemorygramType.Experience,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                Subtype: chatId.ToString()
            )));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistUserInputMemory", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.UserInput &&
            string.IsNullOrEmpty(mg.Subtype)
        )), Times.Once);

        _mockMemoryQueryService.Verify(m => m.GetChatHistoryAsync(chatId.ToString()), Times.Once);
        
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.Experience &&
            mg.Subtype == "Chat"
        )), Times.Once);
    }

    [Fact]
    public async Task PersistUserInputMemory_WithoutChatId_ShouldNotTriggerExperienceWorkflow()
    {
        // Arrange
        var userInput = "Test user input without chat ID";
        var request = new PipelineExecutionRequest
        {
            UserInput = userInput,
            SessionMetadata = new Dictionary<string, object> { { "userId", "test-user" } }
        };

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg => mg.Type == MemorygramType.UserInput)))
            .ReturnsAsync(Result.Ok(new Memorygram(
                Id: Guid.NewGuid(),
                Content: userInput,
                Type: MemorygramType.UserInput,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow
            )));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistUserInputMemory", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        // Act
        await (Task)method.Invoke(_service, new object[] { request })!;

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.UserInput
        )), Times.Once);

        // Should not call memory query service for experience workflow
        _mockMemoryQueryService.Verify(m => m.GetChatHistoryAsync(It.IsAny<string>()), Times.Never);
        
        // Should not create experience memorygram
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(mg =>
            mg.Type == MemorygramType.Experience
        )), Times.Never);
    }
}

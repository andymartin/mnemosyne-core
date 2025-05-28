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
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
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
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<ResponderService>>();
        _service = new ResponderService(
            _mockPipelineExecutorService.Object,
            _mockPipelinesRepository.Object,
            _mockPromptConstructor.Object,
            _mockLanguageModelService.Object,
            _mockReflectiveResponder.Object,
            _mockMemoryQueryService.Object,
            _mockMemorygramService.Object,
            _mockEmbeddingService.Object,
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
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
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
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(llmResponse))
            .ReturnsAsync(Result.Ok(embedding));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), llmResponse, MemorygramType.AssistantResponse, embedding, "LLM_Response", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

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
        _mockEmbeddingService.Verify(e => e.GetEmbeddingAsync(llmResponse), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenGetPipelineFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var errorMessage = "Pipeline not found";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Fail<PipelineManifest>(errorMessage));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Never);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(It.IsAny<PipelineExecutionState>()), Times.Never);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<LanguageModelType>()), Times.Never);
        _mockEmbeddingService.Verify(e => e.GetEmbeddingAsync(userInput), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenPipelineExecutionFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var errorMessage = "Pipeline execution error";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Fail<PipelineExecutionState>(errorMessage));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(It.IsAny<PipelineExecutionState>()), Times.Never);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<LanguageModelType>()), Times.Never);
        _mockEmbeddingService.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>()), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenPromptConstructionFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var pipelineExecutionState = new PipelineExecutionState { RunId = Guid.NewGuid() };
        var errorMessage = "Prompt construction error";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()))
            .ReturnsAsync(Result.Ok(pipelineExecutionState));
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(pipelineExecutionState))
            .Returns(Result.Fail<PromptConstructionResult>(errorMessage));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(pipelineExecutionState), Times.Once);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<LanguageModelType>()), Times.Never);
        _mockEmbeddingService.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>()), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldReturnFailure_WhenLlmGenerationFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
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
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<PipelineExecutionState>()), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(pipelineExecutionState), Times.Once);
        _mockLanguageModelService.Verify(l => l.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master), Times.Once);
        _mockEmbeddingService.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>()), Times.Once);
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldLogWarning_WhenEmbeddingServiceFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Pipeline" };
        var pipelineExecutionState = new PipelineExecutionState { RunId = Guid.NewGuid() };
        var chatCompletionRequest = new ChatCompletionRequest { Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "test" } } };
        var llmResponse = "LLM generated response";
        var embeddingErrorMessage = "Embedding service error";

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
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(llmResponse))
            .ReturnsAsync(Result.Fail<float[]>(embeddingErrorMessage));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), llmResponse, MemorygramType.AssistantResponse, Array.Empty<float>(), "LLM_Response", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Response.ShouldBe(llmResponse);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Failed to generate embedding for response")),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);

        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldLogWarning_WhenMemorygramCreationFails()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var userInput = "Test user input";
        var request = new PipelineExecutionRequest { PipelineId = pipelineId, UserInput = userInput };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
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
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(llmResponse))
            .ReturnsAsync(Result.Ok(embedding));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Fail<Memorygram>(memorygramErrorMessage));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Response.ShouldBe(llmResponse);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Failed to create memorygram from response")),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);

        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessRequestAsync_ShouldAddUserInputExactlyOnceToContext()
    {
        // Arrange
        var userInput = "Test user input";
        var chatId = "test-chat-id";
        var pipelineId = Guid.NewGuid();
        var request = new PipelineExecutionRequest
        {
            UserInput = userInput,
            PipelineId = pipelineId,
            SessionMetadata = new Dictionary<string, object> { { "chatId", chatId } }
        };

        var pipeline = new PipelineManifest
        {
            Id = pipelineId,
            Name = "Test Pipeline",
            Description = "Test Description",
            Components = new List<ComponentConfiguration>()
        };

        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var expectedResponse = "Test response";
        var systemPrompt = "Test system prompt";

        // Mock existing chat history that includes the same user input
        var existingChatHistory = new List<Memorygram>
        {
            new Memorygram(
                Id: Guid.NewGuid(),
                Content: userInput, // Same content as current request
                Type: MemorygramType.UserInput,
                VectorEmbedding: embedding,
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
                UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
                ChatId: chatId
            )
        };

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(pipeline));
        _mockMemoryQueryService.Setup(m => m.GetChatHistoryAsync(chatId))
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
                        Type = ContextChunkType.UserInput,
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
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(userInput))
            .ReturnsAsync(Result.Ok(embedding));
        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(expectedResponse))
            .ReturnsAsync(Result.Ok(embedding));
        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), userInput, MemorygramType.UserInput, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Verify that the pipeline executor was called with state containing exactly one instance of the user input
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.Is<PipelineExecutionState>(state =>
            state.Context.Count(chunk => chunk.Type == ContextChunkType.UserInput && chunk.Content == userInput) == 1
        )), Times.Once);
    }

    [Fact]
    public async Task PersistResponseMemory_WithChatIdInSessionMetadata_ShouldIncludeChatIdInMemorygram()
    {
        // Arrange
        var chatId = "test-chat-id-123";
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>
            {
                ["chatId"] = chatId,
                ["userId"] = "test-user"
            }
        };

        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(response))
            .ReturnsAsync(Result.Ok(embedding));

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, chatId)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request });

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.AssistantResponse &&
                memorygram.ChatId == chatId &&
                memorygram.Content == response &&
                memorygram.VectorEmbedding == embedding &&
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
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

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

        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(response))
            .ReturnsAsync(Result.Ok(embedding));

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request });

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.AssistantResponse &&
                memorygram.ChatId == null &&
                memorygram.Content == response &&
                memorygram.VectorEmbedding == embedding
            )
        ), Times.Once);
    }

    [Fact]
    public async Task PersistResponseMemory_WithNullChatIdInSessionMetadata_ShouldCreateMemorygramWithNullChatId()
    {
        // Arrange
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>
            {
                ["chatId"] = null,
                ["userId"] = "test-user"
            }
        };

        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(response))
            .ReturnsAsync(Result.Ok(embedding));

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request });

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram =>
                memorygram.Type == MemorygramType.AssistantResponse &&
                memorygram.ChatId == null &&
                memorygram.Content == response &&
                memorygram.VectorEmbedding == embedding
            )
        ), Times.Once);
    }

    [Fact]
    public async Task PersistResponseMemory_WhenEmbeddingServiceFails_ShouldNotCreateMemorygram()
    {
        // Arrange
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();
        var errorMessage = "Embedding generation failed";

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>
            {
                ["chatId"] = "test-chat-id"
            }
        };

        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(response))
            .ReturnsAsync(Result.Fail<float[]>(errorMessage));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request });

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Never);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Failed to generate embedding for response")),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task PersistResponseMemory_ShouldSetCorrectMemorygramType()
    {
        // Arrange
        var response = "This is a test assistant response";
        var pipelineId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        var request = new PipelineExecutionRequest
        {
            PipelineId = pipelineId,
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>()
        };

        _mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(response))
            .ReturnsAsync(Result.Ok(embedding));

        _mockMemorygramService.Setup(m => m.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()))
            .ReturnsAsync(Result.Ok(new Memorygram(Guid.NewGuid(), response, MemorygramType.AssistantResponse, embedding, "ResponderService", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        // Use reflection to call the private method
        var method = typeof(ResponderService).GetMethod("PersistResponseMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)method.Invoke(_service, new object[] { response, request });

        // Assert
        _mockMemorygramService.Verify(m => m.CreateOrUpdateMemorygramAsync(
            It.Is<Memorygram>(memorygram => memorygram.Type == MemorygramType.AssistantResponse)
        ), Times.Once);
    }
}
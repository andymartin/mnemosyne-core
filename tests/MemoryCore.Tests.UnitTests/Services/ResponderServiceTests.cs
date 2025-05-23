using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;

namespace MemoryCore.Tests.UnitTests.Services;

public class ResponderServiceTests
{
    private readonly Mock<IPipelineExecutorService> _mockPipelineExecutorService;
    private readonly Mock<IPipelinesRepository> _mockPipelinesRepository;
    private readonly Mock<IPromptConstructor> _mockPromptConstructor;
    private readonly Mock<ILogger<ResponderService>> _mockLogger;
    private readonly ResponderService _service;

    public ResponderServiceTests()
    {
        _mockPipelineExecutorService = new Mock<IPipelineExecutorService>();
        _mockPipelinesRepository = new Mock<IPipelinesRepository>();
        _mockPromptConstructor = new Mock<IPromptConstructor>();
        _mockLogger = new Mock<ILogger<ResponderService>>();
        _service = new ResponderService(
            _mockPipelineExecutorService.Object,
            _mockPipelinesRepository.Object,
            _mockPromptConstructor.Object,
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
        var constructedPrompt = "Constructed prompt content";

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(pipelineId, request))
            .ReturnsAsync(Result.Ok(pipelineExecutionState));
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(pipelineExecutionState))
            .Returns(Result.Ok(constructedPrompt));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe($"Mocked Master LLM response for prompt: '{constructedPrompt}'");

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(pipelineId, request), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(pipelineExecutionState), Times.Once);
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

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(It.IsAny<Guid>(), It.IsAny<PipelineExecutionRequest>()), Times.Never);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(It.IsAny<PipelineExecutionState>()), Times.Never);
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
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(pipelineId, request))
            .ReturnsAsync(Result.Fail<PipelineExecutionState>(errorMessage));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(pipelineId, request), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(It.IsAny<PipelineExecutionState>()), Times.Never);
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
        _mockPipelineExecutorService.Setup(e => e.ExecutePipelineAsync(pipelineId, request))
            .ReturnsAsync(Result.Ok(pipelineExecutionState));
        _mockPromptConstructor.Setup(p => p.ConstructPrompt(pipelineExecutionState))
            .Returns(Result.Fail<string>(errorMessage));

        // Act
        var result = await _service.ProcessRequestAsync(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(errorMessage));

        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
        _mockPipelineExecutorService.Verify(e => e.ExecutePipelineAsync(pipelineId, request), Times.Once);
        _mockPromptConstructor.Verify(p => p.ConstructPrompt(pipelineExecutionState), Times.Once);
    }
}
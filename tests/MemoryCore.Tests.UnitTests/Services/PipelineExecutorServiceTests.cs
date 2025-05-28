using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MemoryCore.Tests.UnitTests.Services;

public class PipelineExecutorServiceTests
{
    private readonly Mock<IPipelinesRepository> _mockPipelinesRepository;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<PipelineExecutorService>> _mockLogger;
    private readonly PipelineExecutorService _service;

    public PipelineExecutorServiceTests()
    {
        _mockPipelinesRepository = new Mock<IPipelinesRepository>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<PipelineExecutorService>>();
        _service = new PipelineExecutorService(
            _mockPipelinesRepository.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecutePipelineAsync_EmptyGuid_ShouldExecuteEmptyPipeline()
    {
        // Arrange
        var emptyPipelineId = Guid.Empty;
        var request = new PipelineExecutionRequest();

        // Act
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = emptyPipelineId,
            Request = request,
            Context = new List<ContextChunk>()
        };
        var result = await _service.ExecutePipelineAsync(state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PipelineId.ShouldBe(emptyPipelineId);
        result.Value.Context.ShouldBeEmpty();
        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ExecutePipelineAsync_ValidGuid_ShouldFetchAndExecutePipeline()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var manifest = new PipelineManifest
        {
            Id = pipelineId,
            Name = "Test Pipeline",
            Components = new List<ComponentConfiguration>
            {
                new ComponentConfiguration { Name = "Test Stage", Type = typeof(NullPipelineStage).FullName! }
            }
        };
        var request = new PipelineExecutionRequest();

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Ok(manifest));
        _mockServiceProvider.Setup(s => s.GetService(typeof(NullPipelineStage)))
            .Returns(new NullPipelineStage());

        // Act
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = pipelineId,
            Request = request,
            Context = new List<ContextChunk>()
        };
        var result = await _service.ExecutePipelineAsync(state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PipelineId.ShouldBe(pipelineId);
        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecutePipelineAsync_PipelineNotFound_ShouldReturnFailure()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var request = new PipelineExecutionRequest();

        _mockPipelinesRepository.Setup(r => r.GetPipelineAsync(pipelineId))
            .ReturnsAsync(Result.Fail<PipelineManifest>("Pipeline manifest not found."));

        // Act
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = pipelineId,
            Request = request,
            Context = new List<ContextChunk>()
        };
        var result = await _service.ExecutePipelineAsync(state);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("Pipeline manifest not found."));
        _mockPipelinesRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
    }
}
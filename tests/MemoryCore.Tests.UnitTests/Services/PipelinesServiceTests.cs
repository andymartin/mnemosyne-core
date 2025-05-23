using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;

namespace MemoryCore.Tests.UnitTests.Services;

public class PipelinesServiceTests
{
    private readonly Mock<IPipelinesRepository> _mockRepository;
    private readonly Mock<ILogger<PipelinesService>> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly PipelinesService _service;

    public PipelinesServiceTests()
    {
        _mockRepository = new Mock<IPipelinesRepository>();
        _mockLogger = new Mock<ILogger<PipelinesService>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _service = new PipelinesService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreatePipelineAsync_ShouldCallRepository_AndReturnResult()
    {
        // Arrange
        var manifest = new PipelineManifest { Name = "Test Create" };
        var expectedResult = Result.Ok(new PipelineManifest { Id = Guid.NewGuid(), Name = "Test Create" });
        _mockRepository.Setup(r => r.CreatePipelineAsync(manifest)).ReturnsAsync(expectedResult);

        // Act
        var result = await _service.CreatePipelineAsync(manifest);

        // Assert
        result.ShouldBe(expectedResult);
        _mockRepository.Verify(r => r.CreatePipelineAsync(manifest), Times.Once);
    }

    [Fact]
    public async Task GetPipelineAsync_ShouldCallRepository_AndReturnResult()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var expectedResult = Result.Ok(new PipelineManifest { Id = pipelineId, Name = "Test Get" });
        _mockRepository.Setup(r => r.GetPipelineAsync(pipelineId)).ReturnsAsync(expectedResult);

        // Act
        var result = await _service.GetPipelineAsync(pipelineId);

        // Assert
        result.ShouldBe(expectedResult);
        _mockRepository.Verify(r => r.GetPipelineAsync(pipelineId), Times.Once);
    }

    [Fact]
    public async Task GetAllPipelinesAsync_ShouldCallRepository_AndReturnResult()
    {
        // Arrange
        var manifests = new List<PipelineManifest> { new PipelineManifest { Id = Guid.NewGuid() } };
        var expectedResult = Result.Ok(manifests.AsEnumerable());
        _mockRepository.Setup(r => r.GetAllPipelinesAsync()).ReturnsAsync(expectedResult);

        // Act
        var result = await _service.GetAllPipelinesAsync();

        // Assert
        result.ShouldBe(expectedResult);
        _mockRepository.Verify(r => r.GetAllPipelinesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdatePipelineAsync_ShouldCallRepository_AndReturnResult()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Update" };
        var expectedResult = Result.Ok(manifest);
        _mockRepository.Setup(r => r.UpdatePipelineAsync(pipelineId, manifest)).ReturnsAsync(expectedResult);

        // Act
        var result = await _service.UpdatePipelineAsync(pipelineId, manifest);

        // Assert
        result.ShouldBe(expectedResult);
        _mockRepository.Verify(r => r.UpdatePipelineAsync(pipelineId, manifest), Times.Once);
    }

    [Fact]
    public async Task DeletePipelineAsync_ShouldCallRepository_AndReturnResult()
    {
        // Arrange
        var pipelineId = Guid.NewGuid();
        var expectedResult = Result.Ok();
        _mockRepository.Setup(r => r.DeletePipelineAsync(pipelineId)).ReturnsAsync(expectedResult);

        // Act
        var result = await _service.DeletePipelineAsync(pipelineId);

        // Assert
        result.ShouldBe(expectedResult);
        _mockRepository.Verify(r => r.DeletePipelineAsync(pipelineId), Times.Once);
    }
}

using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;

namespace Mnemosyne.Core.Tests.Services
{
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
            _service = new PipelinesService(_mockRepository.Object, _mockServiceProvider.Object, _mockLogger.Object);
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

        [Fact]
        public async Task ExecutePipelineAsync_WhenManifestNotFound_ShouldReturnFail()
        {
            // Arrange
            var pipelineId = Guid.NewGuid();
            var request = new PipelineExecutionRequest { UserInput = "test" };
            _mockRepository.Setup(r => r.GetPipelineAsync(pipelineId)).ReturnsAsync(Result.Fail<PipelineManifest>("Not Found"));

            // Act
            var result = await _service.ExecutePipelineAsync(pipelineId, request);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe("Not Found");
        }

        [Fact]
        public async Task ExecutePipelineAsync_WhenManifestFound_ShouldReturnPendingStatusAndStartSimulation()
        {
            // Arrange
            var pipelineId = Guid.NewGuid();
            var manifest = new PipelineManifest
            {
                Id = pipelineId,
                Name = "Test Exec",
                Components = new List<ComponentConfiguration> { new ComponentConfiguration { Name = "Stage1", Type = "NullStage" } }
            };
            var request = new PipelineExecutionRequest { UserInput = "test" };
            _mockRepository.Setup(r => r.GetPipelineAsync(pipelineId)).ReturnsAsync(Result.Ok(manifest));
            
            // Mock IServiceProvider to return a NullPipelineStage for any IPipelineStage request
            // This part is tricky as GetKeyedService is not standard. Assuming a simple resolve for now.
            var mockComponent = new NullPipelineStage();
             _mockServiceProvider.Setup(sp => sp.GetService(typeof(IPipelineStage)))
                                .Returns(mockComponent); // Simplified; real DI might need more setup for keyed services


            // Act
            var result = await _service.ExecutePipelineAsync(pipelineId, request);
            await Task.Delay(1000); // Give simulation time to run a bit (adjust if needed)

            // Assert
            result.IsSuccess.ShouldBeTrue();
            var status = result.Value;
            status.ShouldNotBeNull();
            status.PipelineId.ShouldBe(pipelineId);
            status.RunId.ShouldNotBe(Guid.Empty);
            status.Status.ShouldBeOneOf(PipelineStatus.Pending, PipelineStatus.Running, PipelineStatus.Processing, PipelineStatus.Completed); // It runs async

            // Check if status was added to active executions (indirectly by trying to get it)
            var getStatusResult = _service.GetExecutionStatus(status.RunId);
            getStatusResult.IsSuccess.ShouldBeTrue();
            getStatusResult.Value.ShouldNotBeNull();
            
            // More detailed checks on status changes would require more control over the async execution
            // or a way to inspect the _activeExecutions dictionary if it were made accessible for tests (not ideal).
        }
        
        [Fact]
        public async Task ExecutePipelineAsync_WithNoComponents_ShouldCompleteQuickly()
        {
            // Arrange
            var pipelineId = Guid.NewGuid();
            var manifest = new PipelineManifest { Id = pipelineId, Name = "Empty Pipeline", Components = new List<ComponentConfiguration>() };
            var request = new PipelineExecutionRequest { UserInput = "test" };
            _mockRepository.Setup(r => r.GetPipelineAsync(pipelineId)).ReturnsAsync(Result.Ok(manifest));

            // Act
            var initialResult = await _service.ExecutePipelineAsync(pipelineId, request);
            await Task.Delay(200); // Short delay for the async task to complete

            // Assert
            initialResult.IsSuccess.ShouldBeTrue();
            var runId = initialResult.Value.RunId;
            var finalStatusResult = _service.GetExecutionStatus(runId);

            finalStatusResult.IsSuccess.ShouldBeTrue();
            finalStatusResult.Value.Status.ShouldBe(PipelineStatus.Completed);
            finalStatusResult.Value.Message.ShouldNotBeNull();
            finalStatusResult.Value.Message.ShouldBe("Pipeline completed: No components to execute.");
        }


        [Fact]
        public async Task GetExecutionStatus_WhenRunIdExists_ShouldReturnStatus()
        {
            // Arrange
            var pipelineId = Guid.NewGuid();
            var manifest = new PipelineManifest { Id = pipelineId, Name = "Test Status", Components = new List<ComponentConfiguration>() };
            var request = new PipelineExecutionRequest { UserInput = "test" };
            _mockRepository.Setup(r => r.GetPipelineAsync(pipelineId)).ReturnsAsync(Result.Ok(manifest));

            // Act
            var execResult = await _service.ExecutePipelineAsync(pipelineId, request);
            var runId = execResult.Value.RunId;
            var statusResult = _service.GetExecutionStatus(runId);

            // Assert
            statusResult.IsSuccess.ShouldBeTrue();
            statusResult.Value.ShouldNotBeNull();
            statusResult.Value.RunId.ShouldBe(runId);
        }

        [Fact]
        public void GetExecutionStatus_WhenRunIdDoesNotExist_ShouldReturnFail()
        {
            // Arrange
            var runId = Guid.NewGuid();

            // Act
            var result = _service.GetExecutionStatus(runId);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe($"Execution status not found for Run ID: {runId}");
        }
    }
}

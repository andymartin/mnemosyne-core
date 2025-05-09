using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;
using Xunit.Abstractions;

namespace Mnemosyne.Core.Tests.Services
{
    public class MemoryQueryServiceTests
    {
        private readonly Mock<IEmbeddingService> _mockEmbeddingService;
        private readonly Mock<IMemorygramRepository> _mockRepository;
        private readonly Mock<ILogger<MemoryQueryService>> _mockLogger;
        private readonly MemoryQueryService _service;
        private readonly ITestOutputHelper _output;

        public MemoryQueryServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockEmbeddingService = new Mock<IEmbeddingService>();
            _mockRepository = new Mock<IMemorygramRepository>();
            _mockLogger = new Mock<ILogger<MemoryQueryService>>();
            _service = new MemoryQueryService(
                _mockEmbeddingService.Object,
                _mockRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task QueryAsync_WithValidInput_ReturnsSuccessResult()
        {
            // Arrange
            var input = new McpQueryInput("test query", 5);
            var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
            var similarMemorygrams = new List<MemorygramWithScore>
            {
                new MemorygramWithScore(
                    Guid.NewGuid(),
                    "Test content",
                    MemorygramType.Chat,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    0.95f)
            };

            _mockEmbeddingService
                .Setup(x => x.GetEmbeddingAsync(input.QueryText))
                .ReturnsAsync(Result.Ok(queryVector));

            _mockRepository
                .Setup(x => x.FindSimilarAsync(queryVector, input.TopK!.Value))
                .ReturnsAsync(Result.Ok(similarMemorygrams.AsEnumerable()));

            // Act
            _output.WriteLine("Executing QueryAsync with valid input");
            var result = await _service.QueryAsync(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Status.ShouldBe("success");
            result.Value.Results.ShouldNotBeNull();
            result.Value.Results.Count.ShouldBe(1);
            result.Value.Message.ShouldBeNull();

            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(input.QueryText), Times.Once);
            _mockRepository.Verify(x => x.FindSimilarAsync(queryVector, input.TopK!.Value), Times.Once);
        }

        [Fact]
        public async Task QueryAsync_WithNullTopK_UsesDefaultValue()
        {
            // Arrange
            var input = new McpQueryInput("test query", null);
            var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
            var similarMemorygrams = new List<MemorygramWithScore>
            {
                new MemorygramWithScore(
                    Guid.NewGuid(),
                    "Test content",
                    MemorygramType.Chat,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    0.95f)
            };

            _mockEmbeddingService
                .Setup(x => x.GetEmbeddingAsync(input.QueryText))
                .ReturnsAsync(Result.Ok(queryVector));

            _mockRepository
                .Setup(x => x.FindSimilarAsync(queryVector, It.IsAny<int>()))
                .ReturnsAsync(Result.Ok(similarMemorygrams.AsEnumerable()));

            // Act
            _output.WriteLine("Executing QueryAsync with null TopK");
            var result = await _service.QueryAsync(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Status.ShouldBe("success");

            // Verify that a default value was used
            _mockRepository.Verify(x => x.FindSimilarAsync(queryVector, It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task QueryAsync_WithEmptyQueryText_ReturnsFailResult()
        {
            // Arrange
            var input = new McpQueryInput("", 5);

            // Act
            _output.WriteLine("Executing QueryAsync with empty query text");
            var result = await _service.QueryAsync(input);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldNotBeEmpty();
            result.Errors.First().Message.ShouldContain("empty");

            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
            _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task QueryAsync_WithInvalidTopK_ReturnsFailResult()
        {
            // Arrange
            var input = new McpQueryInput("test query", 0);

            // Act
            _output.WriteLine("Executing QueryAsync with invalid TopK");
            var result = await _service.QueryAsync(input);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldNotBeEmpty();
            result.Errors.First().Message.ShouldContain("greater than 0");

            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
            _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task QueryAsync_WhenEmbeddingServiceFails_ReturnsFailResult()
        {
            // Arrange
            var input = new McpQueryInput("test query", 5);
            var errorMessage = "Embedding service error";

            _mockEmbeddingService
                .Setup(x => x.GetEmbeddingAsync(input.QueryText))
                .ReturnsAsync(Result.Fail(new Error(errorMessage)));

            // Act
            _output.WriteLine("Executing QueryAsync when embedding service fails");
            var result = await _service.QueryAsync(input);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldNotBeEmpty();
            result.Errors.First().Message.ShouldContain(errorMessage);

            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(input.QueryText), Times.Once);
            _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task QueryAsync_WhenRepositoryFails_ReturnsFailResult()
        {
            // Arrange
            var input = new McpQueryInput("test query", 5);
            var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
            var errorMessage = "Repository error";

            _mockEmbeddingService
                .Setup(x => x.GetEmbeddingAsync(input.QueryText))
                .ReturnsAsync(Result.Ok(queryVector));

            _mockRepository
                .Setup(x => x.FindSimilarAsync(queryVector, input.TopK!.Value))
                .ReturnsAsync(Result.Fail(new Error(errorMessage)));

            // Act
            _output.WriteLine("Executing QueryAsync when repository fails");
            var result = await _service.QueryAsync(input);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldNotBeEmpty();
            result.Errors.First().Message.ShouldContain(errorMessage);

            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(input.QueryText), Times.Once);
            _mockRepository.Verify(x => x.FindSimilarAsync(queryVector, input.TopK!.Value), Times.Once);
        }

        [Fact]
        public async Task QueryAsync_WhenExceptionOccurs_ReturnsFailResult()
        {
            // Arrange
            var input = new McpQueryInput("test query", 5);
            var exceptionMessage = "Test exception";

            _mockEmbeddingService
                .Setup(x => x.GetEmbeddingAsync(input.QueryText))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            _output.WriteLine("Executing QueryAsync when exception occurs");
            var result = await _service.QueryAsync(input);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldNotBeEmpty();
            result.Errors.First().Message.ShouldContain(exceptionMessage);

            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(input.QueryText), Times.Once);
            _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>()), Times.Never);
        }
    }
}
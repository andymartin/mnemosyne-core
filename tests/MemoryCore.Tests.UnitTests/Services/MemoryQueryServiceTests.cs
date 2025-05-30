using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;
using Xunit.Abstractions;

namespace MemoryCore.Tests.UnitTests.Services;

public class MemoryQueryServiceTests
{
    private readonly Mock<ISemanticReformulator> _mockSemanticReformulator;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IMemorygramRepository> _mockRepository;
    private readonly Mock<ILogger<MemoryQueryService>> _mockLogger;
    private readonly MemoryQueryService _service;
    private readonly ITestOutputHelper _output;

    public MemoryQueryServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockSemanticReformulator = new Mock<ISemanticReformulator>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockRepository = new Mock<IMemorygramRepository>();
        _mockLogger = new Mock<ILogger<MemoryQueryService>>();
        _service = new MemoryQueryService(
            _mockSemanticReformulator.Object,
            _mockEmbeddingService.Object,
            _mockRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task QueryAsync_WithValidInput_ReturnsSuccessResult()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", 5, MemoryReformulationType.Content, null);
        var reformulations = new MemoryReformulations
        {
            Topical = "original",
            Content = "keyword",
            Context = "paraphrase",
            Metadata = "question"
        };
        _mockSemanticReformulator.Setup(x => x.ReformulateForQueryAsync(input.QueryText)).ReturnsAsync(Result.Ok(reformulations));

        var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
        var similarMemorygrams = new List<MemorygramWithScore>
        {
            new MemorygramWithScore(
                Guid.NewGuid(),
                "Test content",
                MemorygramType.UserInput,
                new float[] { 0.1f, 0.2f, 0.3f },
                new float[] { 0.1f, 0.2f, 0.3f },
                new float[] { 0.1f, 0.2f, 0.3f },
                new float[] { 0.1f, 0.2f, 0.3f },
                "TestSource",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                0.95f)
        };

        _mockEmbeddingService
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Ok(queryVector));
 
        _mockRepository
            .Setup(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), It.IsAny<Guid?>()))
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

        _mockSemanticReformulator.Verify(x => x.ReformulateForQueryAsync(input.QueryText), Times.Once);
        _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Exactly(Enum.GetValues(typeof(MemoryReformulationType)).Length));
        _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), input.TopK!.Value, (Guid?)null), Times.Exactly(Enum.GetValues(typeof(MemoryReformulationType)).Length));
    }

    [Fact]
    public async Task QueryAsync_WithNullTopK_UsesDefaultValue()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", null, MemoryReformulationType.Content, null);
        var reformulations = new MemoryReformulations
        {
            Topical = "original",
            Content = "keyword",
            Context = "paraphrase",
            Metadata = "question"
        };
        _mockSemanticReformulator.Setup(x => x.ReformulateForQueryAsync(input.QueryText)).ReturnsAsync(Result.Ok(reformulations));

        var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
        var similarMemorygrams = new List<MemorygramWithScore>
        {
            new MemorygramWithScore(
                Guid.NewGuid(),
                "Test content",
                MemorygramType.UserInput,
                new float[] { 0.1f, 0.2f, 0.3f },
                new float[] { 0.1f, 0.2f, 0.3f },
                new float[] { 0.1f, 0.2f, 0.3f },
                new float[] { 0.1f, 0.2f, 0.3f },
                "TestSource",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                0.95f)
        };

        _mockEmbeddingService
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Ok(queryVector));
 
        _mockRepository
            .Setup(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), (Guid?)null))
            .ReturnsAsync(Result.Ok(similarMemorygrams.AsEnumerable()));

        // Act
        _output.WriteLine("Executing QueryAsync with null TopK");
        var result = await _service.QueryAsync(input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Status.ShouldBe("success");

        // Verify that a default value was used
        _mockSemanticReformulator.Verify(x => x.ReformulateForQueryAsync(input.QueryText), Times.Once);
        _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Exactly(Enum.GetValues(typeof(MemoryReformulationType)).Length));
        _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), (Guid?)null), Times.Exactly(Enum.GetValues(typeof(MemoryReformulationType)).Length));
    }

    [Fact]
    public async Task QueryAsync_WithEmptyQueryText_ReturnsFailResult()
    {
        // Arrange
        var input = new MemoryQueryInput("", 5, MemoryReformulationType.Content, null);

        // Act
        _output.WriteLine("Executing QueryAsync with empty query text");
        var result = await _service.QueryAsync(input);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().Message.ShouldContain("empty");

        _mockSemanticReformulator.Verify(x => x.ReformulateForQueryAsync(It.IsAny<string>()), Times.Never);
        _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task QueryAsync_WithInvalidTopK_ReturnsFailResult()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", 0, MemoryReformulationType.Content, null);

        // Act
        _output.WriteLine("Executing QueryAsync with invalid TopK");
        var result = await _service.QueryAsync(input);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().Message.ShouldContain("greater than 0");

        _mockSemanticReformulator.Verify(x => x.ReformulateForQueryAsync(It.IsAny<string>()), Times.Never);
        _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task QueryAsync_WhenSemanticReformulatorFails_ReturnsFailResult()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", 5, MemoryReformulationType.Content, null);
        var errorMessage = "Semantic reformulator error";
 
        _mockSemanticReformulator
            .Setup(x => x.ReformulateForQueryAsync(input.QueryText))
            .ReturnsAsync(Result.Fail(new Error(errorMessage)));

        // Act
        _output.WriteLine("Executing QueryAsync when semantic reformulator fails");
        var result = await _service.QueryAsync(input);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().Message.ShouldContain("Failed to reformulate query.");

        _mockSemanticReformulator.Verify(x => x.ReformulateForQueryAsync(input.QueryText), Times.Once);
        _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task QueryAsync_WhenEmbeddingServiceFails_ReturnsSuccessWithEmptyResults()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", 5, MemoryReformulationType.Content, null);
        var reformulations = new MemoryReformulations
        {
            Topical = "original",
            Content = "keyword",
            Context = "paraphrase",
            Metadata = "question"
        };
        _mockSemanticReformulator.Setup(x => x.ReformulateForQueryAsync(input.QueryText)).ReturnsAsync(Result.Ok(reformulations));
        var errorMessage = "Embedding service error for a reformulation";
 
        _mockEmbeddingService
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Fail(new Error(errorMessage)));

        // Act
        _output.WriteLine("Executing QueryAsync when embedding service fails");
        var result = await _service.QueryAsync(input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Status.ShouldBe("success");
        result.Value.Results.ShouldNotBeNull();
        result.Value.Results.Count.ShouldBe(0); // No results because all embedding calls failed

        _mockSemanticReformulator.Verify(x => x.ReformulateForQueryAsync(input.QueryText), Times.Once);
        _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Exactly(Enum.GetValues(typeof(MemoryReformulationType)).Length));
        _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task QueryAsync_WhenExceptionOccurs_ReturnsFailResult()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", 5, MemoryReformulationType.Content, null);
        var exceptionMessage = "Test exception";
 
        _mockSemanticReformulator
            .Setup(x => x.ReformulateForQueryAsync(input.QueryText))
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        _output.WriteLine("Executing QueryAsync when exception occurs");
        var result = await _service.QueryAsync(input);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().Message.ShouldContain(exceptionMessage);

        _mockSemanticReformulator.Verify(x => x.ReformulateForQueryAsync(input.QueryText), Times.Once);
        _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(x => x.FindSimilarAsync(It.IsAny<float[]>(), It.IsAny<MemoryReformulationType>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }
}
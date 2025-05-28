using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using Moq;
using System;
using System.Threading.Tasks;

namespace MemoryCore.Tests.UnitTests.Services;

public class MemorygramServiceTests
{
    private readonly Mock<IMemorygramRepository> _repositoryMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ISemanticReformulator> _semanticReformulatorMock;
    private readonly Mock<ILogger<MemorygramService>> _loggerMock;
    private readonly MemorygramService _service;

    public MemorygramServiceTests()
    {
        _repositoryMock = new Mock<IMemorygramRepository>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _semanticReformulatorMock = new Mock<ISemanticReformulator>();
        _loggerMock = new Mock<ILogger<MemorygramService>>();

        _service = new MemorygramService(
            _repositoryMock.Object,
            _embeddingServiceMock.Object,
            _semanticReformulatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateOrUpdateMemorygramAsync_PopulatesAllEmbeddings_WhenReformulationAndEmbeddingSucceed()
    {
        // Arrange
        var memorygramId = Guid.NewGuid();
        var originalContent = "Test content";
        var memorygram = new Memorygram(
            memorygramId,
            originalContent,
            MemorygramType.UserInput,
            Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), // Embeddings will be populated
            "Test",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var topicalReformulation = "Topical text";
        var contentReformulation = "Content text";
        var contextReformulation = "Context text";
        var metadataReformulation = "Metadata text";

        var reformulations = new MemoryReformulations
        {
            Topical = topicalReformulation,
            Content = contentReformulation,
            Context = contextReformulation,
            Metadata = metadataReformulation
        };

        _semanticReformulatorMock
            .Setup(s => s.ReformulateForStorageAsync(originalContent))
            .ReturnsAsync(Result.Ok(reformulations));

        var topicalEmbedding = new float[] { 0.1f };
        var contentEmbedding = new float[] { 0.2f };
        var contextEmbedding = new float[] { 0.3f };
        var metadataEmbedding = new float[] { 0.4f };

        _embeddingServiceMock
            .Setup(s => s.GetEmbeddingAsync(topicalReformulation))
            .ReturnsAsync(Result.Ok(topicalEmbedding));
        _embeddingServiceMock
            .Setup(s => s.GetEmbeddingAsync(contentReformulation))
            .ReturnsAsync(Result.Ok(contentEmbedding));
        _embeddingServiceMock
            .Setup(s => s.GetEmbeddingAsync(contextReformulation))
            .ReturnsAsync(Result.Ok(contextEmbedding));
        _embeddingServiceMock
            .Setup(s => s.GetEmbeddingAsync(metadataReformulation))
            .ReturnsAsync(Result.Ok(metadataEmbedding));

        var expectedMemorygram = memorygram with {
            TopicalEmbedding = topicalEmbedding,
            ContentEmbedding = contentEmbedding,
            ContextEmbedding = contextEmbedding,
            MetadataEmbedding = metadataEmbedding
        };

        _repositoryMock
            .Setup(r => r.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(m =>
                m.Id == memorygramId &&
                m.TopicalEmbedding == topicalEmbedding &&
                m.ContentEmbedding == contentEmbedding &&
                m.ContextEmbedding == contextEmbedding &&
                m.MetadataEmbedding == metadataEmbedding)))
            .ReturnsAsync(Result.Ok(expectedMemorygram));

        // Act
        var result = await _service.CreateOrUpdateMemorygramAsync(memorygram);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedMemorygram, result.Value);

        _semanticReformulatorMock.Verify(s => s.ReformulateForStorageAsync(originalContent), Times.Once);
        _embeddingServiceMock.Verify(s => s.GetEmbeddingAsync(topicalReformulation), Times.Once);
        _embeddingServiceMock.Verify(s => s.GetEmbeddingAsync(contentReformulation), Times.Once);
        _embeddingServiceMock.Verify(s => s.GetEmbeddingAsync(contextReformulation), Times.Once);
        _embeddingServiceMock.Verify(s => s.GetEmbeddingAsync(metadataReformulation), Times.Once);
        _repositoryMock.Verify(r => r.CreateOrUpdateMemorygramAsync(expectedMemorygram), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateMemorygramAsync_ReturnsFailure_WhenSemanticReformulatorFails()
    {
        // Arrange
        var memorygram = new Memorygram(Guid.NewGuid(), "Test content", MemorygramType.UserInput, Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), "Test", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var reformulationError = "Reformulation error";

        _semanticReformulatorMock
            .Setup(s => s.ReformulateForStorageAsync(memorygram.Content))
            .ReturnsAsync(Result.Fail<MemoryReformulations>(reformulationError));

        // Act
        var result = await _service.CreateOrUpdateMemorygramAsync(memorygram);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains(reformulationError, result.Errors[0].Message);
        _embeddingServiceMock.Verify(s => s.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        _repositoryMock.Verify(r => r.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()), Times.Never);
    }
    
    [Fact]
    public async Task CreateOrUpdateMemorygramAsync_ProceedsWithPartialEmbeddings_WhenOneEmbeddingServiceCallFails()
    {
        // Arrange
        var memorygramId = Guid.NewGuid();
        var originalContent = "Test content";
        var memorygram = new Memorygram(
            memorygramId,
            originalContent,
            MemorygramType.UserInput,
            Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(),
            "Test",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var topicalReformulation = "Topical text";
        var contentReformulation = "Content text (will fail)";
        var contextReformulation = "Context text";
        
        var reformulations = new MemoryReformulations
        {
            Topical = topicalReformulation,
            Content = contentReformulation,
            Context = contextReformulation,
            Metadata = string.Empty
        };

        _semanticReformulatorMock
            .Setup(s => s.ReformulateForStorageAsync(originalContent))
            .ReturnsAsync(Result.Ok(reformulations));

        var topicalEmbedding = new float[] { 0.1f };
        var contextEmbedding = new float[] { 0.3f };
        var embeddingError = "Embedding service error for content";

        _embeddingServiceMock
            .Setup(s => s.GetEmbeddingAsync(topicalReformulation))
            .ReturnsAsync(Result.Ok(topicalEmbedding));
        _embeddingServiceMock
            .Setup(s => s.GetEmbeddingAsync(contentReformulation))
            .ReturnsAsync(Result.Fail<float[]>(embeddingError)); // This one fails
        _embeddingServiceMock
            .Setup(s => s.GetEmbeddingAsync(contextReformulation))
            .ReturnsAsync(Result.Ok(contextEmbedding));
        // Metadata reformulation is null, so GetEmbeddingAsync won't be called for it.

        var expectedMemorygramAfterEmbeddings = memorygram with {
            TopicalEmbedding = topicalEmbedding,
            ContentEmbedding = Array.Empty<float>(), // Failed
            ContextEmbedding = contextEmbedding,
            MetadataEmbedding = Array.Empty<float>() // Reformulation was null or empty
        };
        
        _repositoryMock
            .Setup(r => r.CreateOrUpdateMemorygramAsync(expectedMemorygramAfterEmbeddings))
            .ReturnsAsync(Result.Ok(expectedMemorygramAfterEmbeddings));

        // Act
        var result = await _service.CreateOrUpdateMemorygramAsync(memorygram);

        // Assert
        Assert.True(result.IsSuccess); // Overall operation succeeds
        Assert.Equal(expectedMemorygramAfterEmbeddings, result.Value); // Check that the returned memorygram has partial embeddings

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() != null && v.ToString()!.Contains($"Failed to generate embedding for {MemoryReformulationType.Content} of memorygram {memorygramId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _repositoryMock.Verify(r => r.CreateOrUpdateMemorygramAsync(expectedMemorygramAfterEmbeddings), Times.Once);
    }
}
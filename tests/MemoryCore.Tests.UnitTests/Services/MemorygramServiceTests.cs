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

    [Fact]
    public async Task CreateRelationshipAsync_ReturnsSuccess_WhenValidParameters()
    {
        // Arrange
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "RELATED_TO";
        var weight = 0.85f;
        var properties = "{\"category\": \"test\"}";
        
        var expectedRelationship = new GraphRelationship(
            Guid.NewGuid(),
            fromId,
            toId,
            relationshipType,
            weight,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            properties,
            true
        );

        _repositoryMock
            .Setup(r => r.CreateRelationshipAsync(fromId, toId, relationshipType, weight, properties))
            .ReturnsAsync(Result.Ok(expectedRelationship));

        // Act
        var result = await _service.CreateRelationshipAsync(fromId, toId, relationshipType, weight, properties);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRelationship, result.Value);
        _repositoryMock.Verify(r => r.CreateRelationshipAsync(fromId, toId, relationshipType, weight, properties), Times.Once);
    }

    [Fact]
    public async Task CreateRelationshipAsync_ReturnsFailure_WhenRelationshipTypeIsEmpty()
    {
        // Arrange
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "";
        var weight = 0.85f;

        // Act
        var result = await _service.CreateRelationshipAsync(fromId, toId, relationshipType, weight);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Relationship type cannot be null or empty", result.Errors[0].Message);
        _repositoryMock.Verify(r => r.CreateRelationshipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateRelationshipAsync_ReturnsFailure_WhenWeightIsOutOfRange()
    {
        // Arrange
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "RELATED_TO";
        var weight = 1.5f; // Invalid weight

        // Act
        var result = await _service.CreateRelationshipAsync(fromId, toId, relationshipType, weight);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Weight must be between 0 and 1", result.Errors[0].Message);
        _repositoryMock.Verify(r => r.CreateRelationshipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRelationshipAsync_ReturnsSuccess_WhenValidParameters()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        var weight = 0.75f;
        var properties = "{\"updated\": true}";
        var isActive = false;

        var expectedRelationship = new GraphRelationship(
            relationshipId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "RELATED_TO",
            weight,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            properties,
            isActive
        );

        _repositoryMock
            .Setup(r => r.UpdateRelationshipAsync(relationshipId, weight, properties, isActive))
            .ReturnsAsync(Result.Ok(expectedRelationship));

        // Act
        var result = await _service.UpdateRelationshipAsync(relationshipId, weight, properties, isActive);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRelationship, result.Value);
        _repositoryMock.Verify(r => r.UpdateRelationshipAsync(relationshipId, weight, properties, isActive), Times.Once);
    }

    [Fact]
    public async Task UpdateRelationshipAsync_ReturnsFailure_WhenWeightIsOutOfRange()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        var weight = -0.5f; // Invalid weight

        // Act
        var result = await _service.UpdateRelationshipAsync(relationshipId, weight);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Weight must be between 0 and 1", result.Errors[0].Message);
        _repositoryMock.Verify(r => r.UpdateRelationshipAsync(It.IsAny<Guid>(), It.IsAny<float?>(), It.IsAny<string>(), It.IsAny<bool?>()), Times.Never);
    }

    [Fact]
    public async Task DeleteRelationshipAsync_ReturnsSuccess_WhenValidId()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.DeleteRelationshipAsync(relationshipId))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await _service.DeleteRelationshipAsync(relationshipId);

        // Assert
        Assert.True(result.IsSuccess);
        _repositoryMock.Verify(r => r.DeleteRelationshipAsync(relationshipId), Times.Once);
    }

    [Fact]
    public async Task GetRelationshipByIdAsync_ReturnsSuccess_WhenValidId()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        var expectedRelationship = new GraphRelationship(
            relationshipId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SIMILAR_TO",
            0.9f,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        _repositoryMock
            .Setup(r => r.GetRelationshipByIdAsync(relationshipId))
            .ReturnsAsync(Result.Ok(expectedRelationship));

        // Act
        var result = await _service.GetRelationshipByIdAsync(relationshipId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRelationship, result.Value);
        _repositoryMock.Verify(r => r.GetRelationshipByIdAsync(relationshipId), Times.Once);
    }

    [Fact]
    public async Task GetRelationshipsByMemorygramIdAsync_ReturnsSuccess_WhenValidId()
    {
        // Arrange
        var memorygramId = Guid.NewGuid();
        var relationships = new List<GraphRelationship>
        {
            new(Guid.NewGuid(), memorygramId, Guid.NewGuid(), "CONNECTS_TO", 0.8f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), memorygramId, "RELATES_TO", 0.7f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        _repositoryMock
            .Setup(r => r.GetRelationshipsByMemorygramIdAsync(memorygramId, true, true))
            .ReturnsAsync(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Act
        var result = await _service.GetRelationshipsByMemorygramIdAsync(memorygramId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(relationships, result.Value);
        _repositoryMock.Verify(r => r.GetRelationshipsByMemorygramIdAsync(memorygramId, true, true), Times.Once);
    }

    [Fact]
    public async Task GetRelationshipsByTypeAsync_ReturnsSuccess_WhenValidType()
    {
        // Arrange
        var relationshipType = "SIMILAR_TO";
        var relationships = new List<GraphRelationship>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), relationshipType, 0.8f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), relationshipType, 0.9f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        _repositoryMock
            .Setup(r => r.GetRelationshipsByTypeAsync(relationshipType))
            .ReturnsAsync(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Act
        var result = await _service.GetRelationshipsByTypeAsync(relationshipType);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(relationships, result.Value);
        _repositoryMock.Verify(r => r.GetRelationshipsByTypeAsync(relationshipType), Times.Once);
    }

    [Fact]
    public async Task GetRelationshipsByTypeAsync_ReturnsFailure_WhenTypeIsEmpty()
    {
        // Arrange
        var relationshipType = "";

        // Act
        var result = await _service.GetRelationshipsByTypeAsync(relationshipType);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Relationship type cannot be null or empty", result.Errors[0].Message);
        _repositoryMock.Verify(r => r.GetRelationshipsByTypeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FindRelationshipsAsync_ReturnsSuccess_WhenValidCriteria()
    {
        // Arrange
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "CONNECTS_TO";
        var minWeight = 0.5f;
        var maxWeight = 0.9f;
        var isActive = true;

        var relationships = new List<GraphRelationship>
        {
            new(Guid.NewGuid(), fromId, toId, relationshipType, 0.7f, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, isActive)
        };

        _repositoryMock
            .Setup(r => r.FindRelationshipsAsync(fromId, toId, relationshipType, minWeight, maxWeight, isActive))
            .ReturnsAsync(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Act
        var result = await _service.FindRelationshipsAsync(fromId, toId, relationshipType, minWeight, maxWeight, isActive);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(relationships, result.Value);
        _repositoryMock.Verify(r => r.FindRelationshipsAsync(fromId, toId, relationshipType, minWeight, maxWeight, isActive), Times.Once);
    }

    [Fact]
    public async Task FindRelationshipsAsync_ReturnsFailure_WhenMinWeightGreaterThanMaxWeight()
    {
        // Arrange
        var minWeight = 0.9f;
        var maxWeight = 0.5f; // Invalid range

        // Act
        var result = await _service.FindRelationshipsAsync(minWeight: minWeight, maxWeight: maxWeight);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Minimum weight cannot be greater than maximum weight", result.Errors[0].Message);
        _repositoryMock.Verify(r => r.FindRelationshipsAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<float?>(), It.IsAny<float?>(), It.IsAny<bool?>()), Times.Never);
    }
}
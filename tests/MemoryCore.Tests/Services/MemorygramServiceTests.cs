using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using Moq;

namespace Mnemosyne.Core.Tests.Services
{
    public class MemorygramServiceTests
    {
        private readonly Mock<IMemorygramRepository> _repositoryMock;
        private readonly Mock<IEmbeddingService> _embeddingServiceMock;
        private readonly Mock<ILogger<MemorygramService>> _loggerMock;
        private readonly MemorygramService _service;

        public MemorygramServiceTests()
        {
            _repositoryMock = new Mock<IMemorygramRepository>();
            _embeddingServiceMock = new Mock<IEmbeddingService>();
            _loggerMock = new Mock<ILogger<MemorygramService>>();
            
            _service = new MemorygramService(
                _repositoryMock.Object,
                _embeddingServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task CreateOrUpdateMemorygramAsync_CallsEmbeddingService_AndPopulatesEmbedding()
        {
            // Arrange
            var memorygram = new Memorygram(
                Guid.NewGuid(),
                "Test content",
                MemorygramType.Chat,
                Array.Empty<float>(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
                
            var embedding = new float[] { 0.1f, 0.2f, 0.3f };
            
            _embeddingServiceMock
                .Setup(s => s.GetEmbeddingAsync(memorygram.Content))
                .ReturnsAsync(Result.Ok(embedding));
                
            var expectedMemorygram = memorygram with { VectorEmbedding = embedding };
            
            _repositoryMock
                .Setup(r => r.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(m => 
                    m.Id == memorygram.Id && 
                    m.Content == memorygram.Content &&
                    m.VectorEmbedding == embedding)))
                .ReturnsAsync(Result.Ok(expectedMemorygram));

            // Act
            var result = await _service.CreateOrUpdateMemorygramAsync(memorygram);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedMemorygram, result.Value);
            
            _embeddingServiceMock.Verify(
                s => s.GetEmbeddingAsync(memorygram.Content),
                Times.Once);
                
            _repositoryMock.Verify(
                r => r.CreateOrUpdateMemorygramAsync(It.Is<Memorygram>(m => 
                    m.Id == memorygram.Id && 
                    m.Content == memorygram.Content &&
                    m.VectorEmbedding == embedding)),
                Times.Once);
        }

        [Fact]
        public async Task CreateOrUpdateMemorygramAsync_ReturnsFailure_WhenEmbeddingServiceFails()
        {
            // Arrange
            var memorygram = new Memorygram(
                Guid.NewGuid(),
                "Test content",
                MemorygramType.Chat,
                Array.Empty<float>(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
                
            var embeddingError = "Embedding service error";
            
            _embeddingServiceMock
                .Setup(s => s.GetEmbeddingAsync(memorygram.Content))
                .ReturnsAsync(Result.Fail<float[]>(embeddingError));

            // Act
            var result = await _service.CreateOrUpdateMemorygramAsync(memorygram);

            // Assert
            Assert.True(result.IsFailed);
            Assert.Contains(embeddingError, result.Errors[0].Message);
            
            _repositoryMock.Verify(
                r => r.CreateOrUpdateMemorygramAsync(It.IsAny<Memorygram>()),
                Times.Never);
        }
    }
}
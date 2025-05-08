using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using MemoryCore.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace MemoryCore.Tests.Services
{
    public class HttpEmbeddingServiceTests
    {
        private readonly Mock<ILogger<HttpEmbeddingService>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly HttpEmbeddingService _service;

        public HttpEmbeddingServiceTests()
        {
            _loggerMock = new Mock<ILogger<HttpEmbeddingService>>();
            _handlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_handlerMock.Object)
            {
                BaseAddress = new Uri("http://test-embedding-service.com")
            };
            _service = new HttpEmbeddingService(_httpClient, _loggerMock.Object);
        }

        [Fact]
        public async Task GetEmbeddingAsync_ReturnsEmbedding_WhenServiceRespondsSuccessfully()
        {
            // Arrange
            var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
            var responseContent = JsonSerializer.Serialize(new { embedding = expectedEmbedding });

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            // Act
            var result = await _service.GetEmbeddingAsync("Test content");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedEmbedding, result.Value);
        }

        [Fact]
        public async Task GetEmbeddingAsync_ReturnsFailure_WhenServiceReturnsNonSuccessStatusCode()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Service error")
                });

            // Act
            var result = await _service.GetEmbeddingAsync("Test content");

            // Assert
            Assert.True(result.IsFailed);
            Assert.Contains("Embedding service error: 500", result.Errors[0].Message);
        }

        [Fact]
        public async Task GetEmbeddingAsync_ReturnsFailure_WhenServiceReturnsEmptyEmbedding()
        {
            // Arrange
            var responseContent = JsonSerializer.Serialize(new { embedding = Array.Empty<float>() });

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            // Act
            var result = await _service.GetEmbeddingAsync("Test content");

            // Assert
            Assert.True(result.IsFailed);
            Assert.Contains("empty or null embedding", result.Errors[0].Message);
        }

        [Fact]
        public async Task GetEmbeddingAsync_ReturnsFailure_WhenHttpRequestFails()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Connection failed"));

            // Act
            var result = await _service.GetEmbeddingAsync("Test content");

            // Assert
            Assert.True(result.IsFailed);
            Assert.Contains("Failed to connect to embedding service", result.Errors[0].Message);
        }
    }
}
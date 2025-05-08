using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentResults;
using MemoryCore.Interfaces;
using MemoryCore.Mcp;
using MemoryCore.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace MemoryCore.Tests.Integration
{
    public class QueryMemoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly ITestOutputHelper _output;
        private readonly HttpClient _client;
        private readonly IServiceProvider _serviceProvider;

        public QueryMemoryIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
            _client = _factory.CreateClient();
            _serviceProvider = _factory.Services;
        }

        [Fact]
        public async Task QueryMemoryTool_EndToEnd_ReturnsExpectedResults()
        {
            // Arrange
            var queryText = "Test query for integration test";
            var topK = 3;
            var input = new McpQueryInput(queryText, topK);

            // First create some test memorygrams in Neo4j to query against
            var memorygrams = await CreateTestMemorygramsAsync();
            _output.WriteLine($"Created {memorygrams.Count} test memorygrams");

            // Get the QueryMemoryTool instance from the service provider
            var queryMemoryTool = _serviceProvider.GetRequiredService<QueryMemoryTool>();
            queryMemoryTool.ShouldNotBeNull();

            // Act
            _output.WriteLine($"Executing QueryMemoryAsync with query: '{queryText}' and topK: {topK}");
            var result = await queryMemoryTool.QueryMemoryAsync(input);

            // Assert
            result.ShouldNotBeNull();
            result.Status.ShouldBe("success");
            result.Results.ShouldNotBeNull();
            result.Message.ShouldBeNull();

            // We should get results back (up to topK)
            result.Results.Count.ShouldBeLessThanOrEqualTo(topK);
            result.Results.Count.ShouldBeGreaterThan(0);

            // Verify result structure
            foreach (var item in result.Results)
            {
                item.Id.ShouldNotBe(Guid.Empty);
                item.Content.ShouldNotBeNullOrWhiteSpace();
                item.Score.ShouldBeGreaterThan(0);
                item.CreatedAt.ShouldNotBe(default);
                item.UpdatedAt.ShouldNotBe(default);
            }

            // Results should be ordered by score (highest first)
            for (int i = 0; i < result.Results.Count - 1; i++)
            {
                result.Results[i].Score.ShouldBeGreaterThanOrEqualTo(result.Results[i + 1].Score);
            }
        }

        [Fact]
        public async Task QueryMemoryTool_WithInvalidTopK_ReturnsError()
        {
            // Arrange
            var queryText = "Test query with invalid topK";
            var topK = 0; // Invalid value
            var input = new McpQueryInput(queryText, topK);

            // Get the QueryMemoryTool instance from the service provider
            var queryMemoryTool = _serviceProvider.GetRequiredService<QueryMemoryTool>();

            // Act
            _output.WriteLine($"Executing QueryMemoryAsync with query: '{queryText}' and invalid topK: {topK}");
            var result = await queryMemoryTool.QueryMemoryAsync(input);

            // Assert
            result.ShouldNotBeNull();
            result.Status.ShouldBe("error");
            result.Results.ShouldBeNull();
            result.Message.ShouldNotBeNullOrWhiteSpace();
            result.Message.ShouldContain("greater than 0");
        }

        [Fact]
        public async Task QueryMemoryTool_WithEmptyQueryText_ReturnsError()
        {
            // Arrange
            var queryText = "";
            var topK = 5;
            var input = new McpQueryInput(queryText, topK);

            // Get the QueryMemoryTool instance from the service provider
            var queryMemoryTool = _serviceProvider.GetRequiredService<QueryMemoryTool>();

            // Act
            _output.WriteLine($"Executing QueryMemoryAsync with empty query text");
            var result = await queryMemoryTool.QueryMemoryAsync(input);

            // Assert
            result.ShouldNotBeNull();
            result.Status.ShouldBe("error");
            result.Results.ShouldBeNull();
            result.Message.ShouldNotBeNullOrWhiteSpace();
            result.Message.ShouldContain("empty");
        }

        [Fact]
        public async Task QueryMemoryTool_VerifyEmbeddingServiceIntegration()
        {
            // Arrange
            var queryText = "Test query to verify embedding service integration";
            var topK = 3;
            var input = new McpQueryInput(queryText, topK);

            // Create test memorygrams
            await CreateTestMemorygramsAsync();

            // Get the embedding service to verify the embedding generation
            var embeddingService = _serviceProvider.GetRequiredService<IEmbeddingService>();
            
            // Act - Get the embedding directly from the service
            var embeddingResult = await embeddingService.GetEmbeddingAsync(queryText);
            
            // Assert - Verify the embedding service returns a valid embedding
            embeddingResult.IsSuccess.ShouldBeTrue();
            var queryVector = embeddingResult.Value;
            queryVector.ShouldNotBeNull();
            queryVector.Length.ShouldBeGreaterThan(0);
            
            // Now test the full query flow
            var queryMemoryTool = _serviceProvider.GetRequiredService<QueryMemoryTool>();
            var result = await queryMemoryTool.QueryMemoryAsync(input);
            
            // Verify the query was successful
            result.ShouldNotBeNull();
            result.Status.ShouldBe("success");
            
            // Verify results have similarity scores
            if (result.Results != null && result.Results.Count > 0)
            {
                foreach (var item in result.Results)
                {
                    item.Score.ShouldBeGreaterThan(0);
                }
            }
        }
        
        [Fact]
        public async Task QueryMemoryTool_WithLargeTopK_LimitsResults()
        {
            // Arrange
            var queryText = "Test query with large topK value";
            var topK = 1000; // Very large value
            var input = new McpQueryInput(queryText, topK);
            
            // Create a small number of test memorygrams
            await CreateTestMemorygramsAsync();
            
            // Get the QueryMemoryTool instance
            var queryMemoryTool = _serviceProvider.GetRequiredService<QueryMemoryTool>();
            
            // Act
            _output.WriteLine($"Executing QueryMemoryAsync with query: '{queryText}' and large topK: {topK}");
            var result = await queryMemoryTool.QueryMemoryAsync(input);
            
            // Assert
            result.ShouldNotBeNull();
            result.Status.ShouldBe("success");
            result.Results.ShouldNotBeNull();
            
            // We should get results back but not more than we created
            result.Results.Count.ShouldBeLessThanOrEqualTo(5); // We created 5 test memorygrams
        }

        private async Task<List<Guid>> CreateTestMemorygramsAsync()
        {
            var createdIds = new List<Guid>();
            var contents = new[]
            {
                "This is a test memorygram for vector similarity search",
                "Another test memorygram with different content",
                "A third memorygram for testing query functionality",
                "Vector search should find this memorygram based on similarity",
                "Completely unrelated content that shouldn't match well"
            };

            var repository = _serviceProvider.GetRequiredService<IMemorygramRepository>();
            
            foreach (var content in contents)
            {
                // Create a memorygram with the test content
                // Create a mock embedding vector that will be replaced by the service
                var mockEmbedding = new float[1024];
                
                var memorygram = new Memorygram(
                    Guid.NewGuid(),
                    content,
                    mockEmbedding,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow
                );

                var result = await repository.CreateOrUpdateMemorygramAsync(memorygram);
                if (result.IsSuccess)
                {
                    createdIds.Add(result.Value.Id);
                }
            }

            return createdIds;
        }
    }
}
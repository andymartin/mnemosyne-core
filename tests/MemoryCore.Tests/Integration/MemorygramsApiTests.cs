using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MemoryCore.Controllers;
using MemoryCore.Models;
using Xunit;
using Shouldly;
using System.Text.Json;

namespace MemoryCore.Tests.Integration
{
    public class MemorygramsApiTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public MemorygramsApiTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        private async Task<Memorygram> CreateMemorygramAndDeserializeAsync(CreateMemorygramRequest request, HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
        {
            var response = await _client.PostAsJsonAsync("/memorygrams", request);
            response.StatusCode.ShouldBe(expectedStatusCode, $"Failed to create memorygram for test setup. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}, Content: {await response.Content.ReadAsStringAsync()}");
            
            var memorygram = await response.Content.ReadFromJsonAsync<Memorygram>();
            memorygram.ShouldNotBeNull("Deserialized memorygram should not be null after a successful creation.");
            return memorygram!;
        }

        [Fact]
        public async Task CreateMemorygram_ReturnsCreatedResult()
        {
            // Arrange
            var request = new CreateMemorygramRequest
            {
                Content = "Test API create content",
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/memorygrams", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.Headers.Location.ShouldNotBeNull();
            
            var content = await response.Content.ReadFromJsonAsync<Memorygram>();
            content.ShouldNotBeNull();
            content!.Id.ShouldNotBe(Guid.Empty);
            content.Content.ShouldBe(request.Content);

            // Expected embedding from MockEmbeddingService
            var expectedEmbedding = new float[1024];
            for (int i = 0; i < expectedEmbedding.Length; i++)
            {
                expectedEmbedding[i] = (float)i / 1000;
            }
            content.VectorEmbedding.ShouldBe(expectedEmbedding);
        }

        [Fact]
        public async Task GetMemorygram_WithExistingId_ReturnsOk()
        {
            // Arrange
            // Create the memorygram first
            var createRequest = new CreateMemorygramRequest
            {
                Content = "Test API get content",
                VectorEmbedding = new float[] { 0.4f, 0.5f, 0.6f }
            };

            var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
            var id = createdMemorygram.Id.ToString();

            // Act
            var response = await _client.GetAsync($"/memorygrams/{id}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            
            var content = await response.Content.ReadFromJsonAsync<Memorygram>();
            content.ShouldNotBeNull();
            content!.Id.ToString().ShouldBe(id);
            content.Content.ShouldBe(createRequest.Content);
            
            // Expected embedding from MockEmbeddingService
            var expectedEmbedding = new float[1024];
            for (int i = 0; i < expectedEmbedding.Length; i++)
            {
                expectedEmbedding[i] = (float)i / 1000;
            }
            content.VectorEmbedding.ShouldBe(expectedEmbedding);
        }

        [Fact]
        public async Task GetMemorygram_WithNonExistingId_ReturnsNotFound()
        {
            // Arrange
            var nonExistingId = Guid.Empty.ToString();

            // Act
            var response = await _client.GetAsync($"/memorygrams/{nonExistingId}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateMemorygram_WithInvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateMemorygramRequest
            {
                Content = ""
            };

            // Act
            var response = await _client.PostAsJsonAsync("/memorygrams", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task PatchMemorygram_WithExistingId_ReturnsOk()
        {
            // Arrange
            // Create the memorygram first
            var createRequest = new CreateMemorygramRequest
            {
                Content = "Test API patch content",
                VectorEmbedding = new float[] { 0.7f, 0.8f, 0.9f }
            };

            var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
            var id = createdMemorygram.Id.ToString();

            // Create patch request with updated content only
            var patchRequest = new UpdateMemorygramRequest
            {
                Content = "Updated via PATCH"
            };

            // Act
            var response = await _client.PatchAsJsonAsync($"/memorygrams/{id}", patchRequest);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            
            var content = await response.Content.ReadFromJsonAsync<Memorygram>();
            content.ShouldNotBeNull();
            content!.Id.ToString().ShouldBe(id);
            content.Content.ShouldBe(patchRequest.Content);

            // Expected embedding from MockEmbeddingService (should be the original one, as PATCH doesn't update it here)
            var expectedEmbedding = new float[1024];
            for (int i = 0; i < expectedEmbedding.Length; i++)
            {
                expectedEmbedding[i] = (float)i / 1000;
            }
            content.VectorEmbedding.ShouldBe(expectedEmbedding);
        }

        [Fact]
        public async Task PatchMemorygram_WithNonExistingId_ReturnsNotFound()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid().ToString();
            var patchRequest = new UpdateMemorygramRequest
            {
                Content = "This won't be updated"
            };

            // Act
            var response = await _client.PatchAsJsonAsync($"/memorygrams/{nonExistingId}", patchRequest);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateAssociation_WithValidIds_ReturnsOk()
        {
            // Arrange
            // Create the source memorygram
            var sourceCreateRequest = new CreateMemorygramRequest
            {
                Content = "Source memorygram for association",
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };

            var sourceMemorygram = await CreateMemorygramAndDeserializeAsync(sourceCreateRequest);
            var sourceId = sourceMemorygram.Id.ToString();

            // Create the target memorygram
            var targetCreateRequest = new CreateMemorygramRequest
            {
                Content = "Target memorygram for association",
                VectorEmbedding = new float[] { 0.4f, 0.5f, 0.6f }
            };
            var targetMemorygram = await CreateMemorygramAndDeserializeAsync(targetCreateRequest);
            var targetId = targetMemorygram.Id;

            // Create association request
            var associationRequest = new CreateAssociationRequest
            {
                TargetId = targetId,
                Weight = 0.75f
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/memorygrams/{sourceId}/associate", associationRequest);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            
            var content = await response.Content.ReadFromJsonAsync<Memorygram>();
            content.ShouldNotBeNull();
            content!.Id.ToString().ShouldBe(sourceId);
        }

        [Fact]
        public async Task CreateAssociation_WithNonExistingSourceId_ReturnsNotFound()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid().ToString();
            
            // Create a valid target memorygram
            var targetCreateRequest = new CreateMemorygramRequest
            {
                Content = "Target memorygram for association",
                VectorEmbedding = new float[] { 0.4f, 0.5f, 0.6f }
            };

            var targetMemorygram = await CreateMemorygramAndDeserializeAsync(targetCreateRequest);
            var targetId = targetMemorygram.Id;

            // Create association request
            var associationRequest = new CreateAssociationRequest
            {
                TargetId = targetId,
                Weight = 0.75f
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/memorygrams/{nonExistingId}/associate", associationRequest);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateAssociation_WithNonExistingTargetId_ReturnsNotFound()
        {
            // Arrange
            // Create the source memorygram
            var sourceCreateRequest = new CreateMemorygramRequest
            {
                Content = "Source memorygram for association",
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };

            var sourceMemorygram = await CreateMemorygramAndDeserializeAsync(sourceCreateRequest);
            var sourceId = sourceMemorygram.Id.ToString();

            // Create association request with non-existing target
            var associationRequest = new CreateAssociationRequest
            {
                TargetId = Guid.NewGuid(),
                Weight = 0.75f
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/memorygrams/{sourceId}/associate", associationRequest);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    
        [Fact]
        public async Task UpdateMemorygram_WithValidRequest_ReturnsOk()
        {
            // Arrange
            // Create the memorygram first
            var createRequest = new CreateMemorygramRequest
            {
                Content = "Test API update content",
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };
    
            var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
            var id = createdMemorygram.Id.ToString();

            // Create update request with new content and embedding
            var updateRequest = new UpdateMemorygramRequest
            {
                Content = "Updated via PUT",
                VectorEmbedding = new float[] { 0.4f, 0.5f, 0.6f }
            };
    
            // Act
            var response = await _client.PutAsJsonAsync($"/memorygrams/{id}", updateRequest);
    
            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            
            var content = await response.Content.ReadFromJsonAsync<Memorygram>();
            content.ShouldNotBeNull();
            content!.Id.ToString().ShouldBe(id);
            content.Content.ShouldBe(updateRequest.Content);

            // Expected embedding from MockEmbeddingService (PUT updates the embedding)
            var expectedEmbedding = new float[1024];
            for (int i = 0; i < expectedEmbedding.Length; i++)
            {
                expectedEmbedding[i] = (float)i / 1000;
            }
            content.VectorEmbedding.ShouldBe(expectedEmbedding);
        }

        [Fact]
        public async Task UpdateMemorygram_WithNonExistingId_ReturnsNotFound()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid().ToString();
            var updateRequest = new UpdateMemorygramRequest
            {
                Content = "This won't be updated",
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };
    
            // Act
            var response = await _client.PutAsJsonAsync($"/memorygrams/{nonExistingId}", updateRequest);
    
            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    
        [Fact]
        public async Task UpdateMemorygram_WithEmptyContent_ReturnsBadRequest()
        {
            // Arrange
            // Create the memorygram first
            var createRequest = new CreateMemorygramRequest
            {
                Content = "Test API update content",
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };
    
            var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
            var id = createdMemorygram.Id.ToString();

            // Create update request with empty content
            var updateRequest = new UpdateMemorygramRequest
            {
                Content = "",
                VectorEmbedding = new float[] { 0.4f, 0.5f, 0.6f }
            };
    
            // Act
            var response = await _client.PutAsJsonAsync($"/memorygrams/{id}", updateRequest);
    
            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    
        [Fact]
        public async Task UpdateMemorygram_WithNoUpdateParameters_ReturnsBadRequest()
        {
            // Arrange
            // Create the memorygram first
            var createRequest = new CreateMemorygramRequest
            {
                Content = "Test API update content",
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };
    
            var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
            var id = createdMemorygram.Id.ToString();

            // Create empty update request
            var updateRequest = new UpdateMemorygramRequest();
    
            // Act
            var response = await _client.PutAsJsonAsync($"/memorygrams/{id}", updateRequest);
    
            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateMemorygram_WithMaxLengthContent_Succeeds()
        {
            // Arrange
            var maxContent = new string('a', 10000); // Assuming 10k is max length
            var request = new CreateMemorygramRequest
            {
                Content = maxContent,
                VectorEmbedding = new float[] { 0.1f, 0.2f, 0.3f }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/memorygrams", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            
            var content = await response.Content.ReadFromJsonAsync<Memorygram>();
            content.ShouldNotBeNull();
            content.Content.ShouldBe(maxContent);
        }

        [Fact]
        public async Task GetMemorygram_WithInvalidIdFormat_ReturnsBadRequest()
        {
            // Arrange
            var invalidId = "not-a-valid-guid";

            // Act
            var response = await _client.GetAsync($"/memorygrams/{invalidId}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }
}

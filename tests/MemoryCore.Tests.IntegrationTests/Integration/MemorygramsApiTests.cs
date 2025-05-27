using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemoryCore.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Mnemosyne.Core.Controllers;
using Mnemosyne.Core.Models;
using Shouldly;
using Xunit.Abstractions;

namespace Mnemosyne.Core.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("TestContainerCollection")]
public class MemorygramsApiTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Neo4jContainerFixture _neo4jFixture;
    private readonly EmbeddingServiceContainerFixture _embeddingFixture;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ITestOutputHelper _output;

    public MemorygramsApiTests(
        Neo4jContainerFixture neo4jFixture,
        EmbeddingServiceContainerFixture embeddingFixture,
        ITestOutputHelper output)
    {
        _neo4jFixture = neo4jFixture;
        _embeddingFixture = embeddingFixture;
        _output = output;

        _factory = new CustomWebApplicationFactory(neo4jFixture, embeddingFixture);
        _client = _factory.CreateClient();
        _jsonOptions = _factory.Services.GetRequiredService<JsonSerializerOptions>(); // Initialize from DI
    }

    private Memorygram DeserializeMemorygram(string json)
    {
        _output.WriteLine($"DEBUG JSON: {json}");

        // Create custom options to handle the enum
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        return JsonSerializer.Deserialize<Memorygram>(json, options)!;
    }

    private async Task<Memorygram> CreateMemorygramAndDeserializeAsync(CreateMemorygramRequest request, HttpStatusCode expectedStatusCode = HttpStatusCode.Created)
    {
        var response = await _client.PostAsJsonAsync("/memorygrams", request); // Uses globally configured options
        response.StatusCode.ShouldBe(expectedStatusCode, $"Failed to create memorygram for test setup. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}, Content: {await response.Content.ReadAsStringAsync()}");

        var jsonString = await response.Content.ReadAsStringAsync();
        var memorygram = DeserializeMemorygram(jsonString);
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
            Type = MemorygramType.UserInput
        };

        // Act
        var response = await _client.PostAsJsonAsync("/memorygrams", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var jsonString = await response.Content.ReadAsStringAsync();
        var content = DeserializeMemorygram(jsonString);
        content.ShouldNotBeNull();
        content!.Id.ShouldNotBe(Guid.Empty);
        content.Content.ShouldBe(request.Content);

        // Verify embedding exists and has the correct dimension
        content.VectorEmbedding.ShouldNotBeNull();
        content.VectorEmbedding.Length.ShouldBe(1024); // Dimension from embedding service
    }

    [Fact]
    public async Task GetMemorygram_WithExistingId_ReturnsOk()
    {
        // Arrange
        // Create the memorygram first
        var createRequest = new CreateMemorygramRequest
        {
            Content = "Test API get content",
            Type = MemorygramType.UserInput
        };

        var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
        var id = createdMemorygram.Id.ToString();

        // Act
        var response = await _client.GetAsync($"/memorygrams/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var jsonString = await response.Content.ReadAsStringAsync();
        var content = DeserializeMemorygram(jsonString);
        content.ShouldNotBeNull();
        content!.Id.ToString().ShouldBe(id);
        content.Content.ShouldBe(createRequest.Content);

        // Verify embedding exists and has the correct dimension
        content.VectorEmbedding.ShouldNotBeNull();
        content.VectorEmbedding.Length.ShouldBe(1024); // Dimension from embedding service
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
            Content = "",
            Type = MemorygramType.UserInput
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
            Type = MemorygramType.UserInput
        };

        var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
        var id = createdMemorygram.Id.ToString();

        // Create patch request with updated content only
        var patchRequest = new UpdateMemorygramRequest
        {
            Content = "Updated via PATCH",
            Type = MemorygramType.UserInput
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/memorygrams/{id}", patchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var jsonString = await response.Content.ReadAsStringAsync();
        var content = DeserializeMemorygram(jsonString);
        content.ShouldNotBeNull();
        content!.Id.ToString().ShouldBe(id);
        content.Content.ShouldBe(patchRequest.Content);

        // Verify embedding exists and has the correct dimension (should be the original one)
        content.VectorEmbedding.ShouldNotBeNull();
        content.VectorEmbedding.Length.ShouldBe(1024); // Dimension from embedding service
    }

    [Fact]
    public async Task PatchMemorygram_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid().ToString();
        var patchRequest = new UpdateMemorygramRequest
        {
            Content = "This won't be updated",
            Type = MemorygramType.UserInput
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
            Type = MemorygramType.UserInput
        };

        var sourceMemorygram = await CreateMemorygramAndDeserializeAsync(sourceCreateRequest);
        var sourceId = sourceMemorygram.Id.ToString();

        // Create the target memorygram
        var targetCreateRequest = new CreateMemorygramRequest
        {
            Content = "Target memorygram for association",
            Type = MemorygramType.UserInput
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

        var jsonString = await response.Content.ReadAsStringAsync();
        var content = DeserializeMemorygram(jsonString);
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
            Type = MemorygramType.UserInput
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
            Type = MemorygramType.UserInput
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
            Type = MemorygramType.UserInput
        };

        var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
        var id = createdMemorygram.Id.ToString();

        // Create update request with new content and embedding
        var updateRequest = new UpdateMemorygramRequest
        {
            Content = "Updated via PUT",
            Type = MemorygramType.UserInput
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/memorygrams/{id}", updateRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var jsonString = await response.Content.ReadAsStringAsync();
        var content = DeserializeMemorygram(jsonString);
        content.ShouldNotBeNull();
        content!.Id.ToString().ShouldBe(id);
        content.Content.ShouldBe(updateRequest.Content);

        // Verify embedding exists and has the correct dimension (PUT updates the embedding)
        content.VectorEmbedding.ShouldNotBeNull();
        content.VectorEmbedding.Length.ShouldBe(1024); // Dimension from embedding service
    }

    [Fact]
    public async Task UpdateMemorygram_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid().ToString();
        var updateRequest = new UpdateMemorygramRequest
        {
            Content = "This won't be updated",
            Type = MemorygramType.UserInput
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
            Content = "Test API update content"
        };

        var createdMemorygram = await CreateMemorygramAndDeserializeAsync(createRequest);
        var id = createdMemorygram.Id.ToString();

        // Create update request with empty content
        var updateRequest = new UpdateMemorygramRequest
        {
            Content = "",
            Type = MemorygramType.UserInput
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
            Type = MemorygramType.UserInput
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
            Type = MemorygramType.UserInput
        };

        // Act
        var response = await _client.PostAsJsonAsync("/memorygrams", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var jsonString = await response.Content.ReadAsStringAsync();
        var content = DeserializeMemorygram(jsonString);
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

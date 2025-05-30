using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemoryCore.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;
using Shouldly;

namespace MemoryCore.Tests.IntegrationTests.Integration;

[Trait("Category", "Integration")]
public class PipelinesControllerIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public PipelinesControllerIntegrationTests()
    {
        var testPipelinesPath = Path.Combine(Directory.GetCurrentDirectory(), "pipelines");
        _factory = new CustomWebApplicationFactory()
            .WithPipelineStoragePath(testPipelinesPath);
        _client = _factory.CreateClient();
        _jsonOptions = _factory.Services.GetRequiredService<JsonSerializerOptions>();
    }

    [Fact]
    public async Task GetAllPipelines_ShouldReturnEmptyList_WhenNoManifestsExist()
    {
        // Act
        var response = await _client.GetAsync("/api/pipelines");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<PipelineManifest>>();
        content.ShouldNotBeNull();
        content.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreatePipeline_ShouldReturnCreatedPipeline_WithGeneratedId()
    {
        // Arrange
        var manifest = new PipelineManifest
        {
            Name = "Test Pipeline",
            Description = "A test pipeline for integration testing",
            Components = new List<ComponentConfiguration>
            {
                new ComponentConfiguration { Name = "Step1", Type = "NullStage" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdManifest = await response.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();
        createdManifest.Id.ShouldNotBe(Guid.Empty);
        createdManifest.Name.ShouldBe("Test Pipeline");
        createdManifest.Description.ShouldBe("A test pipeline for integration testing");
        createdManifest.Components.Count.ShouldBe(1);

        // Location header should point to the created resource
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldBe($"/api/pipelines/{createdManifest.Id}");
    }

    [Fact]
    public async Task GetPipeline_ShouldReturnPipeline_WhenItExists()
    {
        // Arrange - Create a pipeline first
        var manifest = new PipelineManifest
        {
            Name = "Retrievable Pipeline",
            Description = "A pipeline to be retrieved"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        // Act
        var response = await _client.GetAsync($"/api/pipelines/{createdManifest.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var retrievedManifest = await response.Content.ReadFromJsonAsync<PipelineManifest>();
        retrievedManifest.ShouldNotBeNull();
        retrievedManifest.Id.ShouldBe(createdManifest.Id);
        retrievedManifest.Name.ShouldBe("Retrievable Pipeline");
    }

    [Fact]
    public async Task GetPipeline_ShouldReturnNotFound_WhenItDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/pipelines/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdatePipeline_ShouldUpdateExistingPipeline()
    {
        // Arrange - Create a pipeline first
        var manifest = new PipelineManifest
        {
            Name = "Original Name",
            Description = "Original description"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        // Prepare update
        var updatedManifest = new PipelineManifest
        {
            Id = createdManifest.Id,
            Name = "Updated Name",
            Description = "Updated description",
            Components = new List<ComponentConfiguration>
            {
                new ComponentConfiguration { Name = "NewStep", Type = "NullStage" }
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/pipelines/{createdManifest.Id}", updatedManifest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returnedManifest = await response.Content.ReadFromJsonAsync<PipelineManifest>();
        returnedManifest.ShouldNotBeNull();
        returnedManifest.Id.ShouldBe(createdManifest.Id);
        returnedManifest.Name.ShouldBe("Updated Name");
        returnedManifest.Description.ShouldBe("Updated description");
        returnedManifest.Components.Count.ShouldBe(1);

        // Verify the update persisted by retrieving it again
        var getResponse = await _client.GetAsync($"/api/pipelines/{createdManifest.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var retrievedManifest = await getResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        retrievedManifest.ShouldNotBeNull();
        retrievedManifest.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task UpdatePipeline_ShouldReturnNotFound_WhenPipelineDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var manifest = new PipelineManifest
        {
            Id = nonExistentId,
            Name = "Won't Be Updated",
            Description = "This update should fail"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/pipelines/{nonExistentId}", manifest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePipeline_ShouldRemovePipeline_WhenItExists()
    {
        // Arrange - Create a pipeline first
        var manifest = new PipelineManifest
        {
            Name = "To Be Deleted",
            Description = "This pipeline will be deleted"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        // Act
        var response = await _client.DeleteAsync($"/api/pipelines/{createdManifest.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/pipelines/{createdManifest.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePipeline_ShouldReturnNotFound_WhenPipelineDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/pipelines/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task GetExecutionStatus_ShouldReturnStatus_WhenRunExists()
    {
        // Arrange
        var manifest = new PipelineManifest
        {
            Name = "Status Test Pipeline",
            Description = "A pipeline for testing status retrieval"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        var runId = Guid.NewGuid();
        var pipelineExecutorService = _factory.Services.GetRequiredService<IPipelineExecutorService>();
        
        var status = new PipelineExecutionStatus
        {
            RunId = runId,
            PipelineId = createdManifest.Id,
            Status = PipelineStatus.Completed,
            OverallStartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(1),
            Message = "Test execution completed"
        };
        
        // Use reflection to add the status to the service's internal dictionary
        var statusDictField = pipelineExecutorService.GetType()
            .GetField("_activeExecutions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var statusDict = statusDictField?.GetValue(pipelineExecutorService) as ConcurrentDictionary<Guid, PipelineExecutionStatus>;
        statusDict?.TryAdd(runId, status);

        // Act
        var response = await _client.GetAsync($"/api/pipelines/{createdManifest.Id}/status/{runId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var retrievedStatus = await response.Content.ReadFromJsonAsync<PipelineExecutionStatus>(_jsonOptions);
        retrievedStatus.ShouldNotBeNull();
        retrievedStatus.RunId.ShouldBe(runId);
        retrievedStatus.PipelineId.ShouldBe(createdManifest.Id);
        retrievedStatus.Status.ShouldBe(PipelineStatus.Completed);
    }

    [Fact]
    public async Task GetExecutionStatus_ShouldReturnNotFound_WhenRunDoesNotExist()
    {
        // Arrange - Create a pipeline first
        var manifest = new PipelineManifest { Name = "Non-existent Run Pipeline" };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        var nonExistentRunId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/pipelines/{createdManifest.Id}/status/{nonExistentRunId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Tests.Fixtures;
using Shouldly;

namespace Mnemosyne.Core.Tests.Integration;

public class PipelinesControllerIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public PipelinesControllerIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory()
            .WithPipelineStoragePath(Path.Combine(Path.GetTempPath(), "PipelineIntegrationTests", Guid.NewGuid().ToString()));
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
    public async Task ExecutePipeline_ShouldReturnAccepted_WithLocationHeader_AndInitialStatus()
    {
        // Arrange - Create a pipeline first
        var manifest = new PipelineManifest
        {
            Name = "Executable Pipeline",
            Description = "A pipeline to be executed",
            Components = new List<ComponentConfiguration>
            {
                new ComponentConfiguration { Name = "TestStage", Type = "NullStage" }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        var executionRequest = new PipelineExecutionRequest
        {
            UserInput = "Test input",
            SessionMetadata = new Dictionary<string, object> { { "SessionId", "test-session" } },
            ResponseChannelMetadata = new Dictionary<string, object> { { "Channel", "test" } }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/pipelines/{createdManifest.Id}/execute", executionRequest);
            
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            
        // Check Location header
        response.Headers.Location.ShouldNotBeNull();
        var locationPath = response.Headers.Location.ToString();
        locationPath.ShouldStartWith($"/api/pipelines/{createdManifest.Id}/status/");
            
        // Check initial status
        var initialStatus = await response.Content.ReadFromJsonAsync<PipelineExecutionStatus>(_jsonOptions);
        initialStatus.ShouldNotBeNull();
        initialStatus.RunId.ShouldNotBe(Guid.Empty);
        initialStatus.PipelineId.ShouldBe(createdManifest.Id);
        initialStatus.Status.ShouldBeOneOf(PipelineStatus.Pending, PipelineStatus.Running, PipelineStatus.Processing);
        initialStatus.OverallStartTime.ShouldNotBe(default(DateTime));
    }

    [Fact]
    public async Task ExecutePipeline_ShouldReturnNotFound_WhenPipelineDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var executionRequest = new PipelineExecutionRequest
        {
            UserInput = "Test input",
            SessionMetadata = new Dictionary<string, object> { { "SessionId", "test-session" } }
        };
            
        // Act
        var response = await _client.PostAsJsonAsync($"/api/pipelines/{nonExistentId}/execute", executionRequest);
            
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExecutionStatus_ShouldReturnStatus_WhenRunExists()
    {
        // Arrange - Create a pipeline and execute it
        var manifest = new PipelineManifest
        {
            Name = "Status Test Pipeline",
            Description = "A pipeline for testing status retrieval"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        var executionRequest = new PipelineExecutionRequest { UserInput = "Status test" };
        var executeResponse = await _client.PostAsJsonAsync($"/api/pipelines/{createdManifest.Id}/execute", executionRequest);
        executeResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            
        var initialStatus = await executeResponse.Content.ReadFromJsonAsync<PipelineExecutionStatus>(_jsonOptions);
        initialStatus.ShouldNotBeNull();

        // Act
        var response = await _client.GetAsync($"/api/pipelines/{createdManifest.Id}/status/{initialStatus.RunId}");
            
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<PipelineExecutionStatus>(_jsonOptions);
        status.ShouldNotBeNull();
        status.RunId.ShouldBe(initialStatus.RunId);
        status.PipelineId.ShouldBe(createdManifest.Id);
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

    [Fact]
    public async Task ExecutePipeline_ShouldEventuallyComplete_AndUpdateStatus()
    {
        // Arrange - Create a pipeline with no components for quick completion
        var manifest = new PipelineManifest
        {
            Name = "Quick Pipeline",
            Description = "A pipeline that should complete quickly",
            Components = new List<ComponentConfiguration>()
        };
        var createResponse = await _client.PostAsJsonAsync("/api/pipelines", manifest, _jsonOptions);
        var createdManifest = await createResponse.Content.ReadFromJsonAsync<PipelineManifest>();
        createdManifest.ShouldNotBeNull();

        var executionRequest = new PipelineExecutionRequest { UserInput = "Quick test" };

        // Act - Execute the pipeline
        var executeResponse = await _client.PostAsJsonAsync($"/api/pipelines/{createdManifest.Id}/execute", executionRequest);
        executeResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            
        var initialStatus = await executeResponse.Content.ReadFromJsonAsync<PipelineExecutionStatus>(_jsonOptions);
        initialStatus.ShouldNotBeNull();

        // Poll for completion (with timeout)
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        PipelineExecutionStatus? finalStatus = null;
            
        while (DateTime.UtcNow - startTime < timeout && (finalStatus == null || finalStatus.Status != PipelineStatus.Completed))
        {
            // Wait a bit between polls
            await Task.Delay(200);
                
            // Get current status
            var statusResponse = await _client.GetAsync($"/api/pipelines/{createdManifest.Id}/status/{initialStatus.RunId}");
            if (statusResponse.IsSuccessStatusCode)
            {
                finalStatus = await statusResponse.Content.ReadFromJsonAsync<PipelineExecutionStatus>(_jsonOptions);
                if (finalStatus != null && finalStatus.Status == PipelineStatus.Completed)
                {
                    break;
                }
            }
        }

        // Assert
        finalStatus.ShouldNotBeNull();
        finalStatus.Status.ShouldBe(PipelineStatus.Completed);
        finalStatus.EndTime.ShouldNotBeNull();
        finalStatus.Result.ShouldNotBeNull();
    }
}

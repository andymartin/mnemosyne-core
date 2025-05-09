using System.Collections.Concurrent;
using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Services;

public class PipelinesService : IPipelinesService
{
    private readonly IPipelinesRepository _repository;
    private readonly ILogger<PipelinesService> _logger;
    private readonly ConcurrentDictionary<Guid, PipelineExecutionStatus> _activeExecutions = new();
    private readonly IServiceProvider _serviceProvider;

    public PipelinesService(
        IPipelinesRepository repository,
        IServiceProvider serviceProvider,
        ILogger<PipelinesService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<PipelineManifest>> CreatePipelineAsync(PipelineManifest manifest)
    {
        if (manifest == null)
        {
            _logger.LogError("Cannot create pipeline: manifest is null");
            return Result.Fail<PipelineManifest>("Pipeline manifest cannot be null");
        }

        _logger.LogInformation("Attempting to create pipeline: {PipelineName}", manifest.Name);
        var createResult = await _repository.CreatePipelineAsync(manifest);
        if (createResult.IsFailed)
        {
            _logger.LogError("Failed to create pipeline {PipelineName}: {Errors}", manifest.Name, string.Join(", ", createResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully created pipeline {PipelineName} with ID {PipelineId}", createResult.Value.Name, createResult.Value.Id);
        }
        return createResult;
    }

    public async Task<Result<PipelineManifest>> GetPipelineAsync(Guid pipelineId)
    {
        _logger.LogInformation("Attempting to get pipeline with ID: {PipelineId}", pipelineId);
        var getResult = await _repository.GetPipelineAsync(pipelineId);
        if (getResult.IsFailed)
        {
            _logger.LogWarning("Failed to get pipeline with ID {PipelineId}: {Errors}", pipelineId, string.Join(", ", getResult.Errors.Select(e => e.Message)));
        }
        return getResult;
    }

    public async Task<Result<IEnumerable<PipelineManifest>>> GetAllPipelinesAsync()
    {
        _logger.LogInformation("Attempting to get all pipelines.");
        var getAllResult = await _repository.GetAllPipelinesAsync();
        if (getAllResult.IsFailed)
        {
            _logger.LogError("Failed to get all pipelines: {Errors}", string.Join(", ", getAllResult.Errors.Select(e => e.Message)));
        }
        return getAllResult;
    }

    public async Task<Result<PipelineManifest>> UpdatePipelineAsync(Guid pipelineId, PipelineManifest manifest)
    {
        _logger.LogInformation("Attempting to update pipeline with ID: {PipelineId}", pipelineId);
        var updateResult = await _repository.UpdatePipelineAsync(pipelineId, manifest);
        if (updateResult.IsFailed)
        {
            _logger.LogError("Failed to update pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", updateResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully updated pipeline {PipelineId}", pipelineId);
        }
        return updateResult;
    }

    public async Task<Result> DeletePipelineAsync(Guid pipelineId)
    {
        _logger.LogInformation("Attempting to delete pipeline with ID: {PipelineId}", pipelineId);
        var deleteResult = await _repository.DeletePipelineAsync(pipelineId);
        if (deleteResult.IsFailed)
        {
            _logger.LogError("Failed to delete pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", deleteResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully deleted pipeline {PipelineId}", pipelineId);
        }
        return deleteResult;
    }

    public async Task<Result<PipelineExecutionStatus>> ExecutePipelineAsync(
        Guid pipelineId,
        PipelineExecutionRequest request)
    {
        _logger.LogInformation("Attempting to execute pipeline ID: {PipelineId} with RunId (to be generated)", pipelineId);

        var manifestResult = await GetPipelineAsync(pipelineId);
        if (manifestResult.IsFailed)
        {
            _logger.LogWarning("Execution failed: Pipeline manifest not found for ID {PipelineId}. Errors: {Errors}", pipelineId, string.Join(", ", manifestResult.Errors.Select(e => e.Message)));
            return Result.Fail<PipelineExecutionStatus>(manifestResult.Errors);
        }
        var manifest = manifestResult.Value;
        var runId = Guid.NewGuid();

        var status = new PipelineExecutionStatus
        {
            RunId = runId,
            PipelineId = pipelineId,
            Status = PipelineStatus.Pending,
            OverallStartTime = DateTime.UtcNow,
            CurrentStageName = "Initializing",
            CurrentStageStartTime = DateTime.UtcNow
        };

        if (!_activeExecutions.TryAdd(runId, status))
        {
            _logger.LogError("Failed to add pipeline execution {RunId} to active executions: Key already exists.", runId);
            return Result.Fail<PipelineExecutionStatus>("Failed to initialize pipeline execution: Duplicate Run ID.");
        }
        _logger.LogInformation("Pipeline ID: {PipelineId} execution initiated with RunId: {RunId}", pipelineId, runId);

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Starting simulated execution for RunId: {RunId}", runId);
                await SimulatePipelineExecution(runId, manifest, request);
                _logger.LogInformation("Finished simulated execution for RunId: {RunId}", runId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during pipeline execution for RunId: {RunId}", runId);
                if (_activeExecutions.TryGetValue(runId, out var errorStatus))
                {
                    errorStatus.Status = PipelineStatus.Failed;
                    errorStatus.Message = ex.ToString();
                    errorStatus.EndTime = DateTime.UtcNow;
                }
            }
        });

        return Result.Ok(status);
    }

    public Result<PipelineExecutionStatus> GetExecutionStatus(Guid runId)
    {
        _logger.LogDebug("Attempting to get execution status for RunId: {RunId}", runId);
        if (_activeExecutions.TryGetValue(runId, out var status))
        {
            return Result.Ok(status);
        }
        _logger.LogWarning("Execution status not found for RunId: {RunId}", runId);
        return Result.Fail<PipelineExecutionStatus>($"Execution status not found for Run ID: {runId}");
    }

    private async Task SimulatePipelineExecution(Guid runId, PipelineManifest manifest, PipelineExecutionRequest initialRequest)
    {
        if (!_activeExecutions.TryGetValue(runId, out var status))
        {
            _logger.LogError("SimulatePipelineExecution: Could not find active execution for RunId {RunId}. Aborting simulation.", runId);
            return;
        }

        var executionState = new PipelineExecutionState
        {
            RunId = runId,
            PipelineId = manifest.Id,
            Request = initialRequest,
        };

        try
        {
            status.Status = PipelineStatus.Running;
            status.CurrentStageName = "StartingExecution";
            status.CurrentStageStartTime = DateTime.UtcNow;
            _logger.LogInformation("RunId {RunId}: Status changed to Running.", runId);

            if (manifest.Components == null || !manifest.Components.Any())
            {
                _logger.LogWarning("RunId {RunId}: Pipeline manifest {PipelineId} has no components defined. Marking as completed.", runId, manifest.Id);
                status.Status = PipelineStatus.Completed;
                status.Result = new PipelineExecutionResult { ResponseMessage = "Pipeline completed: No components to execute." };
                status.EndTime = DateTime.UtcNow;
                return;
            }

            foreach (var componentConfig in manifest.Components)
            {
                status.Status = PipelineStatus.Processing; // Changed from Running to Processing for a stage
                status.CurrentStageName = componentConfig.Name ?? "Unnamed Stage";
                status.CurrentStageStartTime = DateTime.UtcNow; // Consider DateTimeOffset.UtcNow
                _logger.LogInformation("RunId {RunId}: Entering stage: {StageName}", runId, status.CurrentStageName);

                // In a real scenario, resolve and execute IPipelineStage here
                // var component = _serviceProvider.GetKeyedService<IPipelineStage>(componentConfig.Type);
                // if (component != null) { 
                //    var stageResult = await component.ExecuteAsync(executionState, status); 
                //    executionState.CurrentResult = stageResult; // Or handle result appropriately
                // } else { throw new InvalidOperationException($"Component type '{componentConfig.Type}' not registered."); }

                await Task.Delay(TimeSpan.FromMilliseconds(200 + Random.Shared.Next(0, 300))); // Simulate work with slight variance

                _logger.LogInformation("RunId {RunId}: Finished stage: {StageName}", runId, status.CurrentStageName);
            }

            status.Status = PipelineStatus.Completed;
            status.CurrentStageName = "Finished";
            status.EndTime = DateTime.UtcNow; // Consider DateTimeOffset.UtcNow
            status.Result = new PipelineExecutionResult
            {
                ResponseMessage = "Simulated pipeline execution completed successfully.",
                UpdatedMetadata = new Dictionary<string, object> { { "simulated", true } }
            };
            _logger.LogInformation("RunId {RunId}: Pipeline execution completed successfully.", runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunId {RunId}: Exception during simulated pipeline execution.", runId);
            status.Status = PipelineStatus.Failed;
            status.Message = ex.ToString();
            status.EndTime = DateTime.UtcNow; // Consider DateTimeOffset.UtcNow
            // No rethrow, allow the background task to complete.
        }
    }
}

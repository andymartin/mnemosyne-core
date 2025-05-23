using System.Collections.Concurrent;
using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Services;

public class PipelineExecutorService : IPipelineExecutorService
{
    private readonly IPipelinesRepository _pipelinesRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PipelineExecutorService> _logger;
    private readonly ConcurrentDictionary<Guid, PipelineExecutionStatus> _activeExecutions = new();

    public PipelineExecutorService(
        IPipelinesRepository pipelinesRepository,
        IServiceProvider serviceProvider,
        ILogger<PipelineExecutorService> logger)
    {
        _pipelinesRepository = pipelinesRepository ?? throw new ArgumentNullException(nameof(pipelinesRepository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<PipelineExecutionState>> ExecutePipelineAsync(Guid pipelineId, PipelineExecutionRequest request)
    {
        _logger.LogInformation("Attempting to execute pipeline ID: {PipelineId}", pipelineId);

        var manifestResult = await _pipelinesRepository.GetPipelineAsync(pipelineId);
        if (manifestResult.IsFailed)
        {
            _logger.LogWarning("Execution failed: Pipeline manifest not found for ID {PipelineId}. Errors: {Errors}", pipelineId, string.Join(", ", manifestResult.Errors.Select(e => e.Message)));
            return Result.Fail<PipelineExecutionState>(manifestResult.Errors);
        }
        var manifest = manifestResult.Value;
        var runId = Guid.NewGuid(); // Consider if RunId should come from request or be generated here.

        var status = new PipelineExecutionStatus
        {
            RunId = runId,
            PipelineId = pipelineId,
            Status = PipelineStatus.Pending,
            OverallStartTime = DateTimeOffset.UtcNow,
            CurrentStageName = "Initializing",
            CurrentStageStartTime = DateTimeOffset.UtcNow
        };

        if (!_activeExecutions.TryAdd(runId, status))
        {
            _logger.LogError("Failed to add pipeline execution {RunId} to active executions: Key already exists.", runId);
            return Result.Fail<PipelineExecutionState>("Failed to initialize pipeline execution: Duplicate Run ID.");
        }
        _logger.LogInformation("Pipeline ID: {PipelineId} execution initiated with RunId: {RunId}", pipelineId, runId);

        var finalState = await RunPipelineInternalAsync(runId, manifest, request, status);

        // Update final status based on the outcome of RunPipelineInternalAsync
        if (_activeExecutions.TryGetValue(runId, out var finalStatus))
        {
            if (finalStatus.Status != PipelineStatus.Failed) // If not already marked as failed by an exception
            {
                finalStatus.Status = PipelineStatus.Completed;
                finalStatus.Message = finalStatus.Message ?? "Pipeline execution completed.";
                finalStatus.EndTime = DateTimeOffset.UtcNow;
            }
        }
        else
        {
            // This case should ideally not happen if TryAdd succeeded and no removal occurred.
            _logger.LogWarning("Could not find active execution status for RunId {RunId} after execution.", runId);
        }

        return Result.Ok(finalState);
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

    private async Task<PipelineExecutionState> RunPipelineInternalAsync(Guid runId, PipelineManifest manifest, PipelineExecutionRequest initialRequest, PipelineExecutionStatus status)
    {
        var executionState = new PipelineExecutionState
        {
            RunId = runId,
            PipelineId = manifest.Id,
            Request = initialRequest,
            Context = new List<ContextChunk>()
        };

        try
        {
            status.Status = PipelineStatus.Running;
            status.CurrentStageName = "StartingExecution";
            status.CurrentStageStartTime = DateTimeOffset.UtcNow;
            _logger.LogInformation("RunId {RunId}: Status changed to Running.", runId);

            if (manifest.Components == null || !manifest.Components.Any())
            {
                _logger.LogWarning("RunId {RunId}: Pipeline manifest {PipelineId} has no components defined. Marking as completed.", runId, manifest.Id);
                status.Status = PipelineStatus.Completed;
                status.Message = "Pipeline completed: No components to execute.";
                status.EndTime = DateTimeOffset.UtcNow;
                return executionState;
            }

            foreach (var componentConfig in manifest.Components)
            {
                status.Status = PipelineStatus.Processing;
                status.CurrentStageName = componentConfig.Name ?? "Unnamed Stage";
                status.CurrentStageStartTime = DateTimeOffset.UtcNow;
                _logger.LogInformation("RunId {RunId}: Entering stage: {StageName} of type {ComponentType}", runId, status.CurrentStageName, componentConfig.Type);

                IPipelineStage? component = null;
                try
                {
                    // Attempt to resolve the service by its string name (Type)
                    // This requires services to be registered with a key if multiple IPipelineStage implementations exist.
                    // Or, more commonly, use a factory or a mapping from string to Type.
                    // For simplicity, if componentConfig.Type is a fully qualified name, ActivatorUtilities might work.
                    var componentType = Type.GetType(componentConfig.Type);
                    if (componentType != null && typeof(IPipelineStage).IsAssignableFrom(componentType))
                    {
                        component = (IPipelineStage?)ActivatorUtilities.CreateInstance(_serviceProvider, componentType);
                    }

                    if (component == null)
                    {
                        // Fallback or specific resolution logic if GetKeyedService is not available/suitable
                        // This might involve a switch statement on componentConfig.Type or a dictionary lookup
                        _logger.LogWarning("RunId {RunId}: Could not resolve component type '{ComponentType}' directly. Attempting specific resolution if applicable or using NullPipelineStage.", runId, componentConfig.Type);
                        // For MVP, if a specific type isn't found, we could default to NullPipelineStage or throw
                        // Defaulting to NullPipelineStage for now to allow pipeline to "complete"
                        if (componentConfig.Type == typeof(NullPipelineStage).FullName || componentConfig.Type == "NullPipelineStage")
                        {
                            component = new NullPipelineStage();
                        }
                        else
                        {
                            // If you have other known types, resolve them here
                            // e.g., if (componentConfig.Type == "MySpecificStage") component = _serviceProvider.GetService<MySpecificStage>();
                        }
                    }

                    if (component != null)
                    {
                        _logger.LogInformation("RunId {RunId}: Executing stage {StageName}", runId, component.Name);
                        executionState = await component.ExecuteAsync(executionState, status);
                        status.AddStageHistory(component.Name, StageResult.Success, $"Stage {component.Name} completed.");
                        _logger.LogInformation("RunId {RunId}: Finished stage: {StageName}", runId, component.Name);
                    }
                    else
                    {
                        var errorMessage = $"Component type '{componentConfig.Type}' not registered or resolved for stage '{componentConfig.Name}'.";
                        _logger.LogError("RunId {RunId}: {ErrorMessage}", runId, errorMessage);
                        status.AddStageHistory(componentConfig.Name ?? "Unknown Stage", StageResult.Error, errorMessage);
                        throw new InvalidOperationException(errorMessage);
                    }
                }
                catch (Exception stageEx)
                {
                    var errorMessage = $"Error executing stage {componentConfig.Name ?? "Unknown Stage"}: {stageEx.Message}";
                    _logger.LogError(stageEx, "RunId {RunId}: {ErrorMessage}", runId, errorMessage);
                    status.AddStageHistory(componentConfig.Name ?? "Unknown Stage", StageResult.Error, errorMessage);
                    throw; // Rethrow to be caught by the outer try-catch
                }
            }

            status.Status = PipelineStatus.Completed;
            status.CurrentStageName = "Finished";
            status.Message = "Pipeline execution completed successfully.";
            _logger.LogInformation("RunId {RunId}: Pipeline execution completed successfully.", runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunId {RunId}: Exception during pipeline execution.", runId);
            status.Status = PipelineStatus.Failed;
            status.Message = ex.Message; // More concise than ex.ToString() for status message
        }
        finally
        {
            status.EndTime = DateTimeOffset.UtcNow;
        }
        return executionState;
    }
}
using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Services;

public class ResponderService : IResponderService
{
    private readonly IPipelineExecutorService _pipelineExecutorService;
    private readonly IPipelinesRepository _pipelinesRepository;
    private readonly IPromptConstructor _promptConstructor;
    private readonly ILogger<ResponderService> _logger;
    // private readonly IMemorygramService _memorygramService; // For actual memory formation

    public ResponderService(
        IPipelineExecutorService pipelineExecutorService,
        IPipelinesRepository pipelinesRepository,
        IPromptConstructor promptConstructor,
        ILogger<ResponderService> logger
        // IMemorygramService memorygramService
        )
    {
        _pipelineExecutorService = pipelineExecutorService ?? throw new ArgumentNullException(nameof(pipelineExecutorService));
        _pipelinesRepository = pipelinesRepository ?? throw new ArgumentNullException(nameof(pipelinesRepository));
        _promptConstructor = promptConstructor ?? throw new ArgumentNullException(nameof(promptConstructor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // _memorygramService = memorygramService ?? throw new ArgumentNullException(nameof(memorygramService));
    }

    public async Task<Result<string>> ProcessRequestAsync(PipelineExecutionRequest request)
    {
        _logger.LogInformation("Reflective Responder received request. UserInput: {UserInput}", request.UserInput);

        // 1. Use IPipelinesRepository to get the pipeline manifest
        var manifestResult = await _pipelinesRepository.GetPipelineAsync(request.PipelineId);
        if (manifestResult.IsFailed)
        {
            _logger.LogError("Failed to retrieve pipeline manifest for ID {PipelineId}: {Errors}", request.PipelineId, string.Join(", ", manifestResult.Errors.Select(e => e.Message)));
            return Result.Fail<string>($"Failed to retrieve pipeline manifest: {string.Join(", ", manifestResult.Errors.Select(e => e.Message))}");
        }
        var manifest = manifestResult.Value;
        _logger.LogInformation("Successfully retrieved pipeline manifest: {PipelineName}", manifest.Name);

        // 2. Invoke the pipeline (CPP) using IPipelineExecutorService
        _logger.LogInformation("Reflective Responder invoking CPP pipeline {PipelineId}", request.PipelineId);
        var pipelineExecutionStateResult = await _pipelineExecutorService.ExecutePipelineAsync(request.PipelineId, request);

        if (pipelineExecutionStateResult.IsFailed)
        {
            _logger.LogError("CPP pipeline execution failed for PipelineId {PipelineId}: {Errors}", request.PipelineId, string.Join(", ", pipelineExecutionStateResult.Errors.Select(e => e.Message)));
            return Result.Fail<string>($"CPP pipeline execution failed: {string.Join(", ", pipelineExecutionStateResult.Errors.Select(e => e.Message))}");
        }

        var pipelineExecutionState = pipelineExecutionStateResult.Value;
        _logger.LogInformation("CPP pipeline {PipelineId} executed successfully. Final RunId: {RunId}", request.PipelineId, pipelineExecutionState.RunId);


        // 3. After the pipeline completes, use the Prompt Constructor to reify the prompt
        _logger.LogInformation("Constructing prompt for RunId: {RunId}", pipelineExecutionState.RunId);
        var promptResult = _promptConstructor.ConstructPrompt(pipelineExecutionState);
        if (promptResult.IsFailed)
        {
            _logger.LogError("Prompt construction failed for RunId {RunId}: {Errors}", pipelineExecutionState.RunId, string.Join(", ", promptResult.Errors.Select(e => e.Message)));
            return Result.Fail<string>($"Prompt construction failed: {string.Join(", ", promptResult.Errors.Select(e => e.Message))}");
        }
        var prompt = promptResult.Value;
        _logger.LogInformation("Prompt constructed successfully for RunId: {RunId}", pipelineExecutionState.RunId);

        // 4. Send prompt to Master LLM (mocked for MVP)
        _logger.LogInformation("Sending prompt to Master LLM (mocked) for RunId: {RunId}", pipelineExecutionState.RunId);
        var masterLlmResponse = $"Mocked Master LLM response for prompt: '{prompt}'"; // Mock response

        // 5. Invoke memory formation for RR's own response (mocked for MVP)
        _logger.LogInformation("Invoking memory formation (mocked) for RunId: {RunId}", pipelineExecutionState.RunId);
        // await _memorygramService.CreateMemorygramAsync(new Memorygram { /* ... details ... */ });

        // 6. Evaluate response (mocked for MVP)
        _logger.LogInformation("Evaluating Master LLM response (mocked) for RunId: {RunId}", pipelineExecutionState.RunId);
        var isResponseSatisfactory = true; // Assume satisfactory for MVP

        if (isResponseSatisfactory)
        {
            _logger.LogInformation("Response evaluated as satisfactory for RunId: {RunId}", pipelineExecutionState.RunId);
            return Result.Ok(masterLlmResponse);
        }
        else
        {
            _logger.LogWarning("Response evaluated as unsatisfactory for RunId: {RunId}. Re-planning needed (mocked).", pipelineExecutionState.RunId);
            // TODO: Implement re-planning logic / loop back to CPP
            return Result.Fail<string>("Response unsatisfactory, re-planning needed (mocked).");
        }
    }
}
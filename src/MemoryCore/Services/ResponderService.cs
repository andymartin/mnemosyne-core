using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mnemosyne.Core.Services;

public class ResponderService : IResponderService
{
    private readonly IPipelineExecutorService _pipelineExecutorService;
    private readonly IPipelinesRepository _pipelinesRepository;
    private readonly IPromptConstructor _promptConstructor;
    private readonly ILanguageModelService _languageModelService;
    private readonly IMemorygramService _memorygramService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<ResponderService> _logger;

    public ResponderService(
        IPipelineExecutorService pipelineExecutorService,
        IPipelinesRepository pipelinesRepository,
        IPromptConstructor promptConstructor,
        ILanguageModelService languageModelService,
        IMemorygramService memorygramService,
        IEmbeddingService embeddingService,
        ILogger<ResponderService> logger)
    {
        _pipelineExecutorService = pipelineExecutorService ?? throw new ArgumentNullException(nameof(pipelineExecutorService));
        _pipelinesRepository = pipelinesRepository ?? throw new ArgumentNullException(nameof(pipelinesRepository));
        _promptConstructor = promptConstructor ?? throw new ArgumentNullException(nameof(promptConstructor));
        _languageModelService = languageModelService ?? throw new ArgumentNullException(nameof(languageModelService));
        _memorygramService = memorygramService ?? throw new ArgumentNullException(nameof(memorygramService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<string>> ProcessRequestAsync(PipelineExecutionRequest request)
    {
        _logger.LogInformation("ResponderService received request. UserInput: {UserInput}", request.UserInput);

        var getManifestResult = await _pipelinesRepository.GetPipelineAsync(request.PipelineId);
        if (getManifestResult.IsFailed)
        {
            _logger.LogError("Failed to retrieve pipeline manifest for ID {PipelineId}: {Errors}",
                request.PipelineId, getManifestResult.Errors);
            return Result.Fail<string>($"Failed to retrieve pipeline manifest: {getManifestResult.Errors.First().Message}");
        }

        var manifest = getManifestResult.Value;
        _logger.LogInformation("Retrieved pipeline manifest: {PipelineName}", manifest.Name);

        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = request.PipelineId,
            Request = request,
            History = new List<PipelineStageHistory>()
        };

        var executionResult = await _pipelineExecutorService.ExecutePipelineAsync(request.PipelineId, request);

        if (executionResult.IsFailed)
        {
            _logger.LogError("Pipeline execution failed: {Errors}", executionResult.Errors);
            return Result.Fail<string>($"Pipeline execution failed: {executionResult.Errors.First().Message}");
        }

        var finalState = executionResult.Value;
        _logger.LogInformation("Pipeline execution completed successfully. Final state context chunks: {Count}", finalState.Context.Count);

        var promptResult = _promptConstructor.ConstructPrompt(finalState);
        if (promptResult.IsFailed)
        {
            _logger.LogError("Failed to construct prompt: {Errors}", promptResult.Errors);
            return Result.Fail<string>($"Failed to construct prompt: {promptResult.Errors.First().Message}");
        }

        var chatCompletionRequest = promptResult.Value;
        _logger.LogInformation("Constructed chat completion request with {MessageCount} messages.", chatCompletionRequest.Messages.Count);

        var llmResponseResult = await _languageModelService.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master);
        if (llmResponseResult.IsFailed)
        {
            _logger.LogError("Failed to generate LLM completion: {Errors}", llmResponseResult.Errors);
            return Result.Fail<string>($"Failed to generate LLM completion: {llmResponseResult.Errors.First().Message}");
        }

        var llmResponse = llmResponseResult.Value;
        _logger.LogInformation("Received LLM response (truncated for log): {Response}", llmResponse.Length > 200 ? llmResponse.Substring(0, 200) + "..." : llmResponse);

        var embeddingResult = await _embeddingService.GetEmbeddingAsync(llmResponse);
        if (embeddingResult.IsFailed)
        {
            _logger.LogWarning("Failed to generate embedding for LLM response: {Errors}", embeddingResult.Errors);
        }
        else
        {
            var memorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: llmResponse,
                Type: MemorygramType.AssistantResponse,
                VectorEmbedding: embeddingResult.Value,
                Source: "LLM_Response",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow
            );

            var memorygramResult = await _memorygramService.CreateOrUpdateMemorygramAsync(memorygram);

            if (memorygramResult.IsFailed)
            {
                _logger.LogWarning("Failed to create memorygram from LLM response: {Errors}", memorygramResult.Errors);
            }
            else
            {
                _logger.LogInformation("Successfully created memorygram from LLM response with ID: {MemorygramId}", memorygramResult.Value.Id);
            }
        }

        return Result.Ok(llmResponse);
    }
}
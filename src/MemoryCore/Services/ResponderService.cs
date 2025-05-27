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
    private readonly IReflectiveResponder _reflectiveResponder;
    private readonly IMemoryQueryService _memoryQueryService;
    private readonly IMemorygramService _memorygramService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<ResponderService> _logger;

    public ResponderService(
        IPipelineExecutorService pipelineExecutorService,
        IPipelinesRepository pipelinesRepository,
        IPromptConstructor promptConstructor,
        ILanguageModelService languageModelService,
        IReflectiveResponder reflectiveResponder,
        IMemoryQueryService memoryQueryService,
        IMemorygramService memorygramService,
        IEmbeddingService embeddingService,
        ILogger<ResponderService> logger)
    {
        _pipelineExecutorService = pipelineExecutorService ?? throw new ArgumentNullException(nameof(pipelineExecutorService));
        _pipelinesRepository = pipelinesRepository ?? throw new ArgumentNullException(nameof(pipelinesRepository));
        _promptConstructor = promptConstructor ?? throw new ArgumentNullException(nameof(promptConstructor));
        _languageModelService = languageModelService ?? throw new ArgumentNullException(nameof(languageModelService));
        _reflectiveResponder = reflectiveResponder ?? throw new ArgumentNullException(nameof(reflectiveResponder));
        _memoryQueryService = memoryQueryService ?? throw new ArgumentNullException(nameof(memoryQueryService));
        _memorygramService = memorygramService ?? throw new ArgumentNullException(nameof(memorygramService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ResponseResult>> ProcessRequestAsync(PipelineExecutionRequest request)
    {
        _logger.LogInformation("ResponderService received request. UserInput: {UserInput}", request.UserInput);

        // 1. Persist user input memory (BEFORE pipeline execution)
        await PersistUserInputMemory(request);

        // Handle nullable pipeline ID
        PipelineManifest manifest;
        Guid actualPipelineId;

        if (request.PipelineId.HasValue)
        {
            // Pipeline ID provided - validate it exists
            actualPipelineId = request.PipelineId.Value;
            var getManifestResult = await _pipelinesRepository.GetPipelineAsync(actualPipelineId);
            if (getManifestResult.IsFailed)
            {
                _logger.LogError("Failed to retrieve pipeline manifest for ID {PipelineId}: {Errors}",
                    actualPipelineId, getManifestResult.Errors);
                return Result.Fail<ResponseResult>($"Failed to retrieve pipeline manifest: {getManifestResult.Errors.First().Message}");
            }
            manifest = getManifestResult.Value;
            _logger.LogInformation("Retrieved pipeline manifest: {PipelineName}", manifest.Name);
        }
        else
        {
            // No pipeline ID provided - use empty pipeline
            actualPipelineId = Guid.Empty;
            manifest = new PipelineManifest
            {
                Id = actualPipelineId,
                Name = "Empty Pipeline",
                Description = "Default empty pipeline for when no pipeline ID is provided",
                Components = new List<ComponentConfiguration>()
            };
            _logger.LogInformation("Using empty pipeline (no pipeline ID provided)");
        }

        // 1. Add chat history to request metadata (NOT associative memories - those come from pipeline stages)
        await AddChatHistoryToRequest(request);

        // 2. Execute pipeline (includes AgenticWorkflowStage)
        var executionResult = await _pipelineExecutorService.ExecutePipelineAsync(actualPipelineId, request);
            
        if (executionResult.IsFailed)
        {
            _logger.LogError("Pipeline execution failed: {Errors}", executionResult.Errors);
            return Result.Fail<ResponseResult>($"Pipeline execution failed: {executionResult.Errors.First().Message}");
        }

        var finalState = executionResult.Value;
        _logger.LogInformation("Pipeline execution completed successfully. Final state context chunks: {Count}", finalState.Context.Count);

        // 3. Construct prompt with entire chat history
        var promptResult = _promptConstructor.ConstructPrompt(finalState);
        if (promptResult.IsFailed)
        {
            _logger.LogError("Failed to construct prompt: {Errors}", promptResult.Errors);
            return Result.Fail<ResponseResult>($"Failed to construct prompt: {promptResult.Errors.First().Message}");
        }

        var promptConstructionResult = promptResult.Value;
        var chatCompletionRequest = promptConstructionResult.Request;
        var systemPrompt = promptConstructionResult.SystemPrompt;
        _logger.LogInformation("Constructed chat completion request with {MessageCount} messages.", chatCompletionRequest.Messages.Count);

        // 4. Call LLM
        var llmResponseResult = await _languageModelService.GenerateCompletionAsync(chatCompletionRequest, LanguageModelType.Master);
        if (llmResponseResult.IsFailed)
        {
            _logger.LogError("Failed to generate LLM completion: {Errors}", llmResponseResult.Errors);
            return Result.Fail<ResponseResult>($"Failed to generate LLM completion: {llmResponseResult.Errors.First().Message}");
        }

        var llmResponse = llmResponseResult.Value;
        _logger.LogInformation("Received LLM response (truncated for log): {Response}", llmResponse.Length > 200 ? llmResponse.Substring(0, 200) + "..." : llmResponse);

        // 5. Response Evaluation (AFTER pipeline completion)
        var evaluationResult = await _reflectiveResponder.EvaluateResponseAsync(
            request.UserInput, llmResponse);
            
        bool shouldDispatch = true; // Default for MVP
        if (evaluationResult.IsSuccess)
        {
            shouldDispatch = evaluationResult.Value.ShouldDispatch;
        }
        
        if (!shouldDispatch)
        {
            // TODO: Handle false evaluation in future iterations
            // For MVP: dispatch anyway with warning
            _logger.LogWarning("ReflectiveResponder suggested not dispatching response, " +
                "but dispatching anyway as MVP placeholder");
        }

        // 6. Dispatch Response
        _logger.LogInformation("Dispatching response for request: {UserInput}", request.UserInput);

        // 7. Memory Persistence (AFTER response dispatch)
        await PersistResponseMemory(llmResponse, request);

        var responseResult = new ResponseResult
        {
            Response = llmResponse,
            SystemPrompt = systemPrompt
        };

        return Result.Ok(responseResult);
    }
    
    private async Task AddChatHistoryToRequest(PipelineExecutionRequest request)
    {
        var chatId = request.SessionMetadata.GetValueOrDefault("chatId")?.ToString();
        
        if (!string.IsNullOrEmpty(chatId))
        {
            // Get conversation history and add to request metadata
            var historyResult = await _memoryQueryService.GetChatHistoryAsync(chatId);
            if (historyResult.IsSuccess)
            {
                request.SessionMetadata["conversationHistory"] = historyResult.Value;
            }
        }
    }
    
    private async Task PersistUserInputMemory(PipelineExecutionRequest request)
    {
        var embeddingResult = await _embeddingService.GetEmbeddingAsync(request.UserInput);
        if (embeddingResult.IsSuccess)
        {
            var chatId = request.SessionMetadata.GetValueOrDefault("chatId")?.ToString();
            var memorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: request.UserInput,
                Type: MemorygramType.UserInput,
                VectorEmbedding: embeddingResult.Value,
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                ChatId: chatId
            );
            
            var memorygramResult = await _memorygramService.CreateOrUpdateMemorygramAsync(memorygram);
            if (memorygramResult.IsFailed)
            {
                _logger.LogWarning("Failed to create memorygram from user input: {Errors}", memorygramResult.Errors);
            }
            else
            {
                _logger.LogInformation("Successfully created memorygram from user input with ID: {MemorygramId}", memorygramResult.Value.Id);
            }
        }
        else
        {
            _logger.LogWarning("Failed to generate embedding for user input: {Errors}", embeddingResult.Errors);
        }
    }

    private async Task PersistResponseMemory(string response, PipelineExecutionRequest request)
    {
        var embeddingResult = await _embeddingService.GetEmbeddingAsync(response);
        if (embeddingResult.IsSuccess)
        {
            var memorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: response,
                Type: MemorygramType.AssistantResponse,
                VectorEmbedding: embeddingResult.Value,
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow
            );
            
            var memorygramResult = await _memorygramService.CreateOrUpdateMemorygramAsync(memorygram);
            if (memorygramResult.IsFailed)
            {
                _logger.LogWarning("Failed to create memorygram from response: {Errors}", memorygramResult.Errors);
            }
            else
            {
                _logger.LogInformation("Successfully created memorygram from response with ID: {MemorygramId}", memorygramResult.Value.Id);
            }
        }
        else
        {
            _logger.LogWarning("Failed to generate embedding for response: {Errors}", embeddingResult.Errors);
        }
    }
}
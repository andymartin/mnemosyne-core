using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;

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
    private readonly ILogger<ResponderService> _logger;

    public ResponderService(
        IPipelineExecutorService pipelineExecutorService,
        IPipelinesRepository pipelinesRepository,
        IPromptConstructor promptConstructor,
        ILanguageModelService languageModelService,
        IReflectiveResponder reflectiveResponder,
        IMemoryQueryService memoryQueryService,
        IMemorygramService memorygramService,
        ILogger<ResponderService> logger)
    {
        _pipelineExecutorService = pipelineExecutorService ?? throw new ArgumentNullException(nameof(pipelineExecutorService));
        _pipelinesRepository = pipelinesRepository ?? throw new ArgumentNullException(nameof(pipelinesRepository));
        _promptConstructor = promptConstructor ?? throw new ArgumentNullException(nameof(promptConstructor));
        _languageModelService = languageModelService ?? throw new ArgumentNullException(nameof(languageModelService));
        _reflectiveResponder = reflectiveResponder ?? throw new ArgumentNullException(nameof(reflectiveResponder));
        _memoryQueryService = memoryQueryService ?? throw new ArgumentNullException(nameof(memoryQueryService));
        _memorygramService = memorygramService ?? throw new ArgumentNullException(nameof(memorygramService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ResponseResult>> ProcessRequestAsync(PipelineExecutionRequest request)
    {
        _logger.LogInformation("ResponderService received request. UserInput: {UserInput}", request.UserInput);

        // 1. Persist user input memory (BEFORE pipeline execution)
        await PersistUserInputMemory(request);

        // Pipeline ID provided - validate it exists
        var pipelineId = request.PipelineId ?? Guid.Empty;
        var manifestResult = await _pipelinesRepository.GetPipelineAsync(pipelineId);
        if (manifestResult.IsFailed)
        {
            _logger.LogError("Failed to retrieve pipeline manifest for ID {PipelineId}: {Errors}",
                pipelineId, manifestResult.Errors);
            return Result.Fail<ResponseResult>($"Failed to retrieve pipeline manifest: {manifestResult.Errors.First().Message}");
        }
        var manifest = manifestResult.Value;
        _logger.LogInformation("Retrieved pipeline manifest: {PipelineName}", manifest.Name);

        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = pipelineId,
            Request = request,
            History = new List<PipelineStageHistory>(),
            Context = new List<ContextChunk>()
        };

        // 1. Add chat history (NOT associative memories - those come from pipeline stages)
        await AddChatHistoryToState(state, request);

        // 2. Execute pipeline (includes AgenticWorkflowStage)
        var executionResult = await _pipelineExecutorService.ExecutePipelineAsync(state);
            
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
    
    private async Task AddChatHistoryToState(PipelineExecutionState state, PipelineExecutionRequest request)
    {
        var chatId = request.SessionMetadata.GetValueOrDefault("chatId")?.ToString();
        
        if (!string.IsNullOrEmpty(chatId))
        {
            // Get conversation history and add as context chunks
            var historyResult = await _memoryQueryService.GetChatHistoryAsync(chatId);
            if (historyResult.IsSuccess)
            {
                foreach (var historyItem in historyResult.Value)
                {
                    state.Context.Add(new ContextChunk
                    {
                        Type = historyItem.Type,
                        Subtype = historyItem.Subtype,
                        Content = historyItem.Content,
                        RelevanceScore = 1.0f,
                        Provenance = new ContextProvenance
                        {
                            Source = ContextProvenance.ChatHistory,
                            Timestamp = historyItem.CreatedAt
                        }
                    });
                }
            }
        }
    }
    
    private async Task PersistUserInputMemory(PipelineExecutionRequest request)
    {
        string? chatIdString = request.SessionMetadata.GetValueOrDefault("chatId")?.ToString();
        Guid? chatId = !string.IsNullOrEmpty(chatIdString) && Guid.TryParse(chatIdString, out var parsedGuid) ? parsedGuid : null;

        var memorygram = new Memorygram(
            Id: Guid.NewGuid(),
            Content: request.UserInput,
            Type: MemorygramType.UserInput,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: "ResponderService",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Subtype: "Chat"
        );

        var memorygramResult = await _memorygramService.CreateOrUpdateMemorygramAsync(memorygram);
        if (memorygramResult.IsFailed)
        {
            _logger.LogWarning("Failed to create memorygram from user input: {Errors}", string.Join(", ", memorygramResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully created memorygram from user input with ID: {MemorygramId}", memorygramResult.Value.Id);
        }

        // Handle experience creation workflow
        if (chatId.HasValue)
        {
            await HandleExperienceCreationWorkflow(chatIdString!, request.UserInput);
        }
    }

    private async Task PersistResponseMemory(string llmResponse, PipelineExecutionRequest request)
    {
        string? chatIdString = request.SessionMetadata.GetValueOrDefault("chatId")?.ToString();
        Guid? chatId = !string.IsNullOrEmpty(chatIdString) && Guid.TryParse(chatIdString, out var parsedGuid) ? parsedGuid : null;

        var memorygram = new Memorygram(
            Id: Guid.NewGuid(),
            Content: llmResponse,
            Type: MemorygramType.AssistantResponse,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: "ResponderService",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Subtype: "Chat"
        );

        var memorygramResult = await _memorygramService.CreateOrUpdateMemorygramAsync(memorygram);
        if (memorygramResult.IsFailed)
        {
            _logger.LogWarning("Failed to create memorygram from LLM response: {Errors}", string.Join(", ", memorygramResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully created memorygram from LLM response with ID: {MemorygramId}", memorygramResult.Value.Id);
        }
    }

    private async Task<bool> IsFirstMessageInChat(string chatId)
    {
        try
        {
            var historyResult = await _memoryQueryService.GetChatHistoryAsync(chatId);
            if (historyResult.IsFailed)
            {
                _logger.LogWarning("Failed to retrieve chat history for experience creation: {Errors}",
                    string.Join(", ", historyResult.Errors.Select(e => e.Message)));
                return true; // Assume first message if we can't check history
            }

            // Count only UserInput and AssistantResponse messages (not Experience messages)
            var conversationMessages = historyResult.Value
                .Where(m => m.Type == MemorygramType.UserInput || m.Type == MemorygramType.AssistantResponse)
                .ToList();

            return conversationMessages.Count <= 1; // First message if only current user input exists
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if first message in chat {ChatId}", chatId);
            return true; // Assume first message on error
        }
    }

    private async Task<Result<Memorygram>> CreateExperienceForChat(string chatId, string userInput)
    {
        try
        {
            var experienceContent = $"New conversation started with: {userInput}";
            
            var experienceMemorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: experienceContent,
                Type: MemorygramType.Experience,
                Subtype: chatId,
                TopicalEmbedding: Array.Empty<float>(),
                ContentEmbedding: Array.Empty<float>(),
                ContextEmbedding: Array.Empty<float>(),
                MetadataEmbedding: Array.Empty<float>(),
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow
            );

            var result = await _memorygramService.CreateOrUpdateMemorygramAsync(experienceMemorygram);
            if (result.IsSuccess)
            {
                _logger.LogInformation("Created new experience for chat {ChatId} with ID: {ExperienceId}",
                    chatId, result.Value.Id);
            }
            else
            {
                _logger.LogError("Failed to create experience for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", result.Errors.Select(e => e.Message)));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating experience for chat {ChatId}", chatId);
            return Result.Fail($"Error creating experience: {ex.Message}");
        }
    }

    private async Task<Result> AssociateWithExistingExperience(string chatId, string userInput)
    {
        try
        {
            // Find existing experiences for this chat
            var historyResult = await _memoryQueryService.GetChatHistoryAsync(chatId);
            if (historyResult.IsFailed)
            {
                _logger.LogWarning("Failed to retrieve chat history for experience association: {Errors}",
                    string.Join(", ", historyResult.Errors.Select(e => e.Message)));
                return Result.Fail("Failed to retrieve chat history");
            }

            var existingExperiences = historyResult.Value
                .Where(m => m.Type == MemorygramType.Experience)
                .OrderByDescending(m => m.CreatedAt)
                .ToList();

            if (!existingExperiences.Any())
            {
                _logger.LogWarning("No existing experience found for chat {ChatId}, creating new one", chatId);
                var createResult = await CreateExperienceForChat(chatId, userInput);
                return createResult.IsSuccess ? Result.Ok() : Result.Fail(createResult.Errors);
            }

            // Use the most recent experience
            var latestExperience = existingExperiences.First();
            
            // Update the experience content to reflect the ongoing conversation
            var updatedContent = $"{latestExperience.Content}\nContinued with: {userInput}";
            
            var updatedExperience = latestExperience with
            {
                Content = updatedContent,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var updateResult = await _memorygramService.CreateOrUpdateMemorygramAsync(updatedExperience);
            if (updateResult.IsSuccess)
            {
                _logger.LogInformation("Updated existing experience {ExperienceId} for chat {ChatId}",
                    latestExperience.Id, chatId);
                return Result.Ok();
            }
            else
            {
                _logger.LogError("Failed to update experience {ExperienceId} for chat {ChatId}: {Errors}",
                    latestExperience.Id, chatId, string.Join(", ", updateResult.Errors.Select(e => e.Message)));
                return Result.Fail(updateResult.Errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error associating with existing experience for chat {ChatId}", chatId);
            return Result.Fail($"Error associating with experience: {ex.Message}");
        }
    }

    private async Task HandleExperienceCreationWorkflow(string chatId, string userInput)
    {
        try
        {
            var isFirstMessage = await IsFirstMessageInChat(chatId);
            
            if (isFirstMessage)
            {
                var createResult = await CreateExperienceForChat(chatId, userInput);
                if (createResult.IsFailed)
                {
                    _logger.LogError("Failed to create experience for new chat {ChatId}: {Errors}",
                        chatId, string.Join(", ", createResult.Errors.Select(e => e.Message)));
                }
            }
            else
            {
                var associateResult = await AssociateWithExistingExperience(chatId, userInput);
                if (associateResult.IsFailed)
                {
                    _logger.LogError("Failed to associate with existing experience for chat {ChatId}: {Errors}",
                        chatId, string.Join(", ", associateResult.Errors.Select(e => e.Message)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in experience creation workflow for chat {ChatId}", chatId);
        }
    }
}

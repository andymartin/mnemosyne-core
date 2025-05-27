using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Services;

public class ChatService : IChatService
{
    private readonly IResponderService _responderService;
    private readonly IMemorygramService _memorygramService;
    private readonly IMemoryQueryService _memoryQueryService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IResponderService responderService,
        IMemorygramService memorygramService,
        IMemoryQueryService memoryQueryService,
        ILogger<ChatService> logger)
    {
        _responderService = responderService ?? throw new ArgumentNullException(nameof(responderService));
        _memorygramService = memorygramService ?? throw new ArgumentNullException(nameof(memorygramService));
        _memoryQueryService = memoryQueryService ?? throw new ArgumentNullException(nameof(memoryQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<string>> ProcessUserMessageAsync(string chatId, string userText, Guid? pipelineId = null)
    {
        try
        {
            _logger.LogInformation("Processing user message for chat {ChatId}", chatId);

            // 1. Store user message memorygram
            var userMemorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: userText,
                Type: MemorygramType.UserInput,
                VectorEmbedding: Array.Empty<float>(),
                Source: "Chat",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                ChatId: chatId
            );

            var userMemorygramResult = await _memorygramService.CreateOrUpdateMemorygramAsync(userMemorygram);

            if (userMemorygramResult.IsFailed)
            {
                _logger.LogError("Failed to store user message for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", userMemorygramResult.Errors.Select(e => e.Message)));
                return Result.Fail<string>("Failed to store user message: " +
                    string.Join(", ", userMemorygramResult.Errors.Select(e => e.Message)));
            }

            _logger.LogInformation("User message stored with ID {UserMemorygramId} for chat {ChatId}",
                userMemorygramResult.Value.Id, chatId);

            // 2. Create PipelineExecutionRequest
            var pipelineRequest = new PipelineExecutionRequest
            {
                PipelineId = pipelineId,
                UserInput = userText,
                SessionMetadata = new Dictionary<string, object>
                {
                    ["chatId"] = chatId,
                    ["userMemorygramId"] = userMemorygramResult.Value.Id
                }
            };

            // 3. Call ResponderService (not AgenticWorkflowService)
            var responseResult = await _responderService.ProcessRequestAsync(pipelineRequest);

            if (responseResult.IsFailed)
            {
                _logger.LogError("Failed to process message with ResponderService for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", responseResult.Errors.Select(e => e.Message)));
                return Result.Fail<string>("Failed to process message with ResponderService: " +
                    string.Join(", ", responseResult.Errors.Select(e => e.Message)));
            }

            _logger.LogInformation("ResponderService processing completed for chat {ChatId}", chatId);

            // 4. Store assistant response memorygram with proper chain linking
            var assistantMemorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: responseResult.Value,
                Type: MemorygramType.AssistantResponse,
                VectorEmbedding: Array.Empty<float>(), // Will be populated by MemorygramService
                Source: "ResponderService",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                ChatId: chatId,
                PreviousMemorygramId: userMemorygramResult.Value.Id // Link to user message
            );

            var assistantMemorygramResult = await _memorygramService.CreateOrUpdateMemorygramAsync(assistantMemorygram);

            if (assistantMemorygramResult.IsFailed)
            {
                _logger.LogError("Failed to store assistant response for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", assistantMemorygramResult.Errors.Select(e => e.Message)));
                return Result.Fail<string>("Failed to store assistant response: " +
                    string.Join(", ", assistantMemorygramResult.Errors.Select(e => e.Message)));
            }

            _logger.LogInformation("Assistant response stored with ID {AssistantMemorygramId} for chat {ChatId}",
                assistantMemorygramResult.Value.Id, chatId);

            // 5. Update conversation chain
            await UpdateConversationChain(userMemorygramResult.Value.Id, assistantMemorygramResult.Value.Id);

            // 6. Return assistant response to client
            _logger.LogInformation("Chat processing completed successfully for chat {ChatId}", chatId);
            return Result.Ok(responseResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user message for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error processing user message: {ex.Message}"));
        }
    }

    private async Task UpdateConversationChain(Guid userMemorygramId, Guid assistantMemorygramId)
    {
        // Create association between consecutive messages
        var associationResult = await _memorygramService.CreateAssociationAsync(
            userMemorygramId,
            assistantMemorygramId,
            1.0f);

        if (associationResult.IsFailed)
        {
            _logger.LogWarning("Failed to create association between user and assistant messages: {Errors}",
                string.Join(", ", associationResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Association created between user and assistant messages");
        }
    }
}
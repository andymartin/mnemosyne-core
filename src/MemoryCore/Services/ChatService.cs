using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public class ChatService : IChatService
{
    private readonly IAgenticWorkflowService _awfService;
    private readonly IMemorygramService _memorygramService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IAgenticWorkflowService awfService,
        IMemorygramService memorygramService,
        ILogger<ChatService> logger)
    {
        _awfService = awfService ?? throw new ArgumentNullException(nameof(awfService));
        _memorygramService = memorygramService ?? throw new ArgumentNullException(nameof(memorygramService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<string>> ProcessUserMessageAsync(string chatId, string userText)
    {
        try
        {
            _logger.LogInformation("Processing user message for chat {ChatId}", chatId);

            // 1. Store user message memorygram
            var userMemorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: userText,
                Type: MemorygramType.UserMessage,
                VectorEmbedding: Array.Empty<float>(), // Will be populated by MemorygramService
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

            // 2. Call AWF with user message ID
            var awfResult = await _awfService.ProcessMessageAsync(
                chatId, 
                userText, 
                userMemorygramResult.Value.Id);

            if (awfResult.IsFailed)
            {
                _logger.LogError("Failed to process message with AWF for chat {ChatId}: {Errors}", 
                    chatId, string.Join(", ", awfResult.Errors.Select(e => e.Message)));
                return Result.Fail<string>("Failed to process message with AWF: " + 
                    string.Join(", ", awfResult.Errors.Select(e => e.Message)));
            }

            _logger.LogInformation("AWF processing completed for chat {ChatId}", chatId);

            // 3. Store assistant response memorygram
            var assistantMemorygram = new Memorygram(
                Id: Guid.NewGuid(),
                Content: awfResult.Value.AssistantResponseText,
                Type: MemorygramType.AssistantResponse,
                VectorEmbedding: Array.Empty<float>(), // Will be populated by MemorygramService
                Source: "AWF",
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

            // 4. Create association between user message and assistant response
            var associationResult = await _memorygramService.CreateAssociationAsync(
                userMemorygramResult.Value.Id,
                assistantMemorygramResult.Value.Id,
                1.0f);

            if (associationResult.IsFailed)
            {
                _logger.LogWarning("Failed to create association between user and assistant messages for chat {ChatId}: {Errors}", 
                    chatId, string.Join(", ", associationResult.Errors.Select(e => e.Message)));
                // Don't fail the entire operation for association failure
            }
            else
            {
                _logger.LogInformation("Association created between user and assistant messages for chat {ChatId}", chatId);
            }

            // 5. Return assistant response to client
            _logger.LogInformation("Chat processing completed successfully for chat {ChatId}", chatId);
            return Result.Ok(awfResult.Value.AssistantResponseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user message for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error processing user message: {ex.Message}"));
        }
    }
}
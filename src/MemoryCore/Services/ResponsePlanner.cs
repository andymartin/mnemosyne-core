using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.AWF;

namespace Mnemosyne.Core.Services;

public class ResponsePlanner : IResponsePlanner
{
    private readonly IMemoryQueryService _memoryQueryService;
    private readonly ILogger<ResponsePlanner> _logger;

    public ResponsePlanner(
        IMemoryQueryService memoryQueryService,
        ILogger<ResponsePlanner> logger)
    {
        _memoryQueryService = memoryQueryService ?? throw new ArgumentNullException(nameof(memoryQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<PlanningContext>> RetrieveAndPrepareContextAsync(
        string chatId, 
        string userText, 
        Guid userMemorygramId)
    {
        try
        {
            _logger.LogInformation("Retrieving and preparing context for chat {ChatId}, user memorygram {UserMemorygramId}", 
                chatId, userMemorygramId);

            // 1. Retrieve thread history from Memory Core
            var threadHistoryResult = await _memoryQueryService.GetChatHistoryAsync(chatId);
            if (threadHistoryResult.IsFailed)
            {
                _logger.LogWarning("Failed to retrieve thread history for chat {ChatId}: {Errors}", 
                    chatId, string.Join(", ", threadHistoryResult.Errors.Select(e => e.Message)));
            }

            // 2. Retrieve relevant associative memories
            var associativeMemoriesResult = await _memoryQueryService.QueryMemoryAsync(userText);
            if (associativeMemoriesResult.IsFailed)
            {
                _logger.LogWarning("Failed to retrieve associative memories for text '{UserText}': {Errors}", 
                    userText, string.Join(", ", associativeMemoriesResult.Errors.Select(e => e.Message)));
            }

            // 3. Combine into a context structure
            var context = new PlanningContext
            {
                ChatId = chatId,
                UserText = userText,
                UserMemorygramId = userMemorygramId,
                ThreadHistory = threadHistoryResult.IsSuccess ? threadHistoryResult.Value : new List<Memorygram>(),
                AssociativeMemories = associativeMemoriesResult.IsSuccess ? associativeMemoriesResult.Value : new List<Memorygram>(),
                UtilizedMemoryIds = new List<Guid>()
            };

            _logger.LogInformation("Context prepared with {ThreadHistoryCount} thread history items and {AssociativeMemoriesCount} associative memories",
                context.ThreadHistory.Count, context.AssociativeMemories.Count);

            return Result.Ok(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving and preparing context for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error retrieving and preparing context: {ex.Message}"));
        }
    }
}
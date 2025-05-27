using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.AWF;

namespace Mnemosyne.Core.Services;

public class AgenticWorkflowService : IAgenticWorkflowService
{
    private readonly IResponsePlanner _responsePlanner;
    private readonly IReflectiveResponder _reflectiveResponder;
    private readonly ILogger<AgenticWorkflowService> _logger;

    public AgenticWorkflowService(
        IResponsePlanner responsePlanner,
        IReflectiveResponder reflectiveResponder,
        ILogger<AgenticWorkflowService> logger)
    {
        _responsePlanner = responsePlanner ?? throw new ArgumentNullException(nameof(responsePlanner));
        _reflectiveResponder = reflectiveResponder ?? throw new ArgumentNullException(nameof(reflectiveResponder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<AWFProcessingResult>> ProcessMessageAsync(
        string chatId, 
        string userText, 
        Guid userMemorygramId)
    {
        try
        {
            _logger.LogInformation("Starting AWF processing for chat {ChatId}, user memorygram {UserMemorygramId}", 
                chatId, userMemorygramId);

            // 1. Response Planner: Retrieve and prepare context
            var contextResult = await _responsePlanner.RetrieveAndPrepareContextAsync(
                chatId, userText, userMemorygramId);

            if (contextResult.IsFailed)
            {
                _logger.LogError("Failed to retrieve and prepare context for chat {ChatId}: {Errors}", 
                    chatId, string.Join(", ", contextResult.Errors.Select(e => e.Message)));
                return Result.Fail<AWFProcessingResult>(contextResult.Errors);
            }

            var context = contextResult.Value;

            // TODO: This service will be deleted in Phase 6 - temporarily disabled
            _logger.LogWarning("AgenticWorkflowService is deprecated and will be removed");
            
            // Return a placeholder result
            var placeholderResult = new AWFProcessingResult
            {
                AssistantResponseText = "Placeholder response - AgenticWorkflowService deprecated",
                UtilizedMemoryIds = new List<Guid>()
            };

            _logger.LogInformation("AWF processing completed successfully for chat {ChatId}, response length: {ResponseLength}",
                chatId, placeholderResult.AssistantResponseText.Length);

            return Result.Ok(placeholderResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AWF processing for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error during AWF processing: {ex.Message}"));
        }
    }
}
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

            // 2. Reflective Responder: Evaluate context
            var evaluationResult = await _reflectiveResponder.EvaluateContextAsync(context);

            if (evaluationResult.IsFailed)
            {
                _logger.LogError("Failed to evaluate context for chat {ChatId}: {Errors}", 
                    chatId, string.Join(", ", evaluationResult.Errors.Select(e => e.Message)));
                return Result.Fail<AWFProcessingResult>(evaluationResult.Errors);
            }

            if (!evaluationResult.Value.ShouldProceedToCpp)
            {
                _logger.LogWarning("Context evaluation determined processing should not proceed for chat {ChatId}", chatId);
                return Result.Fail<AWFProcessingResult>("Context evaluation determined processing should not proceed.");
            }

            // 3. Reflective Responder: Process with CPP
            var processingResult = await _reflectiveResponder.ProcessWithCppAsync(context);

            if (processingResult.IsFailed)
            {
                _logger.LogError("Failed to process with CPP for chat {ChatId}: {Errors}", 
                    chatId, string.Join(", ", processingResult.Errors.Select(e => e.Message)));
                return Result.Fail<AWFProcessingResult>(processingResult.Errors);
            }

            // 4. Reflective Responder: Perform post-response analysis
            var reflectionResult = await _reflectiveResponder.PerformPostResponseAnalysisAsync(
                userText, 
                processingResult.Value.AssistantResponseText,
                processingResult.Value.UtilizedMemoryIds);

            if (reflectionResult.IsFailed)
            {
                _logger.LogError("Failed to perform post-response analysis for chat {ChatId}: {Errors}", 
                    chatId, string.Join(", ", reflectionResult.Errors.Select(e => e.Message)));
                return Result.Fail<AWFProcessingResult>(reflectionResult.Errors);
            }

            // 5. Return result to calling service (Chat Service) for persistence
            var awfResult = new AWFProcessingResult
            {
                AssistantResponseText = processingResult.Value.AssistantResponseText,
                ReflectionData = reflectionResult.Value,
                UtilizedMemoryIds = processingResult.Value.UtilizedMemoryIds
            };

            _logger.LogInformation("AWF processing completed successfully for chat {ChatId}, response length: {ResponseLength}", 
                chatId, awfResult.AssistantResponseText.Length);

            return Result.Ok(awfResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AWF processing for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error during AWF processing: {ex.Message}"));
        }
    }
}
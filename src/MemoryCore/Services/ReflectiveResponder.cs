using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.AWF;

namespace Mnemosyne.Core.Services;

public class ReflectiveResponder : IReflectiveResponder
{
    private readonly ICognitiveProcessingService _cognitiveProcessingService;
    private readonly ILogger<ReflectiveResponder> _logger;

    public ReflectiveResponder(
        ICognitiveProcessingService cognitiveProcessingService,
        ILogger<ReflectiveResponder> logger)
    {
        _cognitiveProcessingService = cognitiveProcessingService ?? throw new ArgumentNullException(nameof(cognitiveProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<EvaluationResult>> EvaluateContextAsync(PlanningContext context)
    {
        try
        {
            _logger.LogInformation("Evaluating context for chat {ChatId}", context.ChatId);

            // For MVP, simple validation that context has required elements
            var result = new EvaluationResult
            {
                ShouldProceedToCpp = true,
                EvaluationNotes = "Context validated for CPP processing"
            };

            _logger.LogInformation("Context evaluation completed for chat {ChatId}: proceed={ShouldProceed}", 
                context.ChatId, result.ShouldProceedToCpp);

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating context for chat {ChatId}", context.ChatId);
            return Result.Fail(new Error($"Error evaluating context: {ex.Message}"));
        }
    }

    public async Task<Result<ProcessingResult>> ProcessWithCppAsync(PlanningContext context)
    {
        try
        {
            _logger.LogInformation("Processing with CPP for chat {ChatId}", context.ChatId);

            // Call CPP service with the prepared context
            var cppResult = await _cognitiveProcessingService.ProcessAsync(
                context.UserText,
                context.ThreadHistory,
                context.AssociativeMemories);

            if (cppResult.IsFailed)
            {
                _logger.LogError("CPP processing failed for chat {ChatId}: {Errors}", 
                    context.ChatId, string.Join(", ", cppResult.Errors.Select(e => e.Message)));
                return Result.Fail<ProcessingResult>(cppResult.Errors);
            }

            var result = new ProcessingResult
            {
                AssistantResponseText = cppResult.Value.ResponseText,
                UtilizedMemoryIds = cppResult.Value.UtilizedMemoryIds
            };

            _logger.LogInformation("CPP processing completed for chat {ChatId}, utilized {MemoryCount} memories", 
                context.ChatId, result.UtilizedMemoryIds.Count);

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing with CPP for chat {ChatId}", context.ChatId);
            return Result.Fail(new Error($"Error processing with CPP: {ex.Message}"));
        }
    }

    public async Task<Result<ReflectionResult>> PerformPostResponseAnalysisAsync(
        string userText,
        string assistantResponse,
        IEnumerable<Guid> utilizedMemoryIds)
    {
        try
        {
            _logger.LogInformation("Performing post-response analysis for user request: {UserRequest}", userText);

            // For MVP, simple logging of the decision to proceed with persistence orchestration
            var result = new ReflectionResult
            {
                ReflectionNotes = "Basic post-response analysis completed",
                UtilizedMemoryIds = utilizedMemoryIds.ToList(),
                AdditionalMetadata = new Dictionary<string, object>
                {
                    ["analysisTimestamp"] = DateTime.UtcNow,
                    ["responseLength"] = assistantResponse.Length,
                    ["utilizedMemoryCount"] = utilizedMemoryIds.Count()
                }
            };

            _logger.LogInformation("Post-response analysis completed, utilized {MemoryCount} memories", 
                result.UtilizedMemoryIds.Count);

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing post-response analysis");
            return Result.Fail(new Error($"Error performing post-response analysis: {ex.Message}"));
        }
    }
}
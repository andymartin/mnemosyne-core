using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

/// <summary>
/// Production implementation of ICognitiveProcessingService that uses the actual Language Model Service
/// </summary>
public class CognitiveProcessingService : ICognitiveProcessingService
{
    private readonly ILanguageModelService _languageModelService;
    private readonly ILogger<CognitiveProcessingService> _logger;

    public CognitiveProcessingService(
        ILanguageModelService languageModelService,
        ILogger<CognitiveProcessingService> logger)
    {
        _languageModelService = languageModelService ?? throw new ArgumentNullException(nameof(languageModelService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CognitiveProcessingResult>> ProcessAsync(
        string userText,
        IEnumerable<Memorygram> threadHistory,
        IEnumerable<Memorygram> associativeMemories)
    {
        try
        {
            _logger.LogInformation("Starting cognitive processing for user text: {UserText}", userText);

            // Build context from thread history and associative memories
            var contextBuilder = new System.Text.StringBuilder();
            
            // Add thread history (recent conversation context)
            var recentHistory = threadHistory.OrderBy(m => m.CreatedAt).Take(10).ToList();
            if (recentHistory.Any())
            {
                contextBuilder.AppendLine("Recent conversation history:");
                foreach (var memory in recentHistory)
                {
                    var role = memory.Type == MemorygramType.UserMessage ? "User" : "Assistant";
                    contextBuilder.AppendLine($"{role}: {memory.Content}");
                }
                contextBuilder.AppendLine();
            }

            // Add relevant associative memories (related context from past conversations)
            var relevantMemories = associativeMemories.Take(5).ToList();
            if (relevantMemories.Any())
            {
                contextBuilder.AppendLine("Relevant memories from past conversations:");
                foreach (var memory in relevantMemories)
                {
                    contextBuilder.AppendLine($"- {memory.Content}");
                }
                contextBuilder.AppendLine();
            }

            // Construct the prompt for the LLM
            var systemPrompt = @"You are Mnemosyne, an AI assistant with advanced memory capabilities. You have access to conversation history and relevant memories from past interactions. Use this context to provide helpful, accurate, and contextually appropriate responses.

Key capabilities:
- Access to conversation history for context
- Ability to recall relevant information from past conversations
- Thoughtful analysis and reasoning
- Clear and helpful communication

When responding:
1. Consider the full context provided
2. Reference relevant memories when appropriate
3. Provide clear, helpful answers
4. Maintain conversation continuity";

            var userPrompt = $@"{contextBuilder}

Current user message: {userText}

Please provide a helpful response based on the context above.";

            // Create the chat completion request
            var chatRequest = new ChatCompletionRequest
            {
                Messages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = userPrompt }
                },
                MaxTokens = 1000,
                Temperature = 0.7f
            };

            _logger.LogInformation("Sending request to Master LLM with {HistoryCount} history items and {MemoryCount} associative memories",
                recentHistory.Count, relevantMemories.Count);

            // Call the Master LLM
            var llmResult = await _languageModelService.GenerateCompletionAsync(chatRequest, "Master");

            if (llmResult.IsFailed)
            {
                _logger.LogError("LLM request failed: {Errors}",
                    string.Join(", ", llmResult.Errors.Select(e => e.Message)));
                return Result.Fail<CognitiveProcessingResult>(llmResult.Errors);
            }

            var assistantResponse = llmResult.Value;

            // Collect the memory IDs that were utilized in this response
            var utilizedMemoryIds = new List<Guid>();
            utilizedMemoryIds.AddRange(recentHistory.Select(m => m.Id));
            utilizedMemoryIds.AddRange(relevantMemories.Select(m => m.Id));

            var result = new CognitiveProcessingResult
            {
                ResponseText = assistantResponse,
                UtilizedMemoryIds = utilizedMemoryIds
            };

            _logger.LogInformation("Cognitive processing completed successfully. Response length: {ResponseLength}, Utilized memories: {MemoryCount}",
                result.ResponseText.Length, result.UtilizedMemoryIds.Count);

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cognitive processing");
            return Result.Fail(new Error($"Cognitive processing error: {ex.Message}"));
        }
    }
}
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

    public async Task<Result<ResponseResult>> ProcessUserMessageAsync(Guid chatId, string userText, Guid? pipelineId = null)
    {
        try
        {
            _logger.LogInformation("Processing user message for chat {ChatId}", chatId);

            var pipelineRequest = new PipelineExecutionRequest
            {
                PipelineId = pipelineId,
                UserInput = userText,
                SessionMetadata = new Dictionary<string, object>
                {
                    ["chatId"] = chatId
                }
            };

            var responseResult = await _responderService.ProcessRequestAsync(pipelineRequest);
            if (responseResult.IsFailed)
            {
                _logger.LogError("Failed to process message with ResponderService for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", responseResult.Errors.Select(e => e.Message)));
                return Result.Fail<ResponseResult>("Failed to process message with ResponderService: " +
                    string.Join(", ", responseResult.Errors.Select(e => e.Message)));
            }

            _logger.LogInformation("ResponderService processing completed for chat {ChatId}", chatId);

            return Result.Ok(responseResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user message for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error processing user message: {ex.Message}"));
        }
    }

    public async Task<Result<List<Memorygram>>> GetChatHistoryAsync(Guid chatId)
    {
        try
        {
            _logger.LogInformation("Retrieving chat history for chat {ChatId} using graph relationships", chatId);

            // First, find the Experience memorygram for this chat using HAS_CHAT_ID relationship
            var experienceResult = await GetExperienceForChatAsync(chatId);
            if (experienceResult.IsFailed)
            {
                _logger.LogError("Failed to find experience for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", experienceResult.Errors.Select(e => e.Message)));
                return Result.Fail<List<Memorygram>>("Failed to find experience for chat");
            }

            if (experienceResult.Value == null)
            {
                _logger.LogWarning("No experience found for chat {ChatId}", chatId);
                return Result.Ok(new List<Memorygram>());
            }

            var experience = experienceResult.Value;

            // Find all memorygrams associated with this experience via ROOT_OF relationships
            var relationshipsResult = await _memorygramService.GetRelationshipsByMemorygramIdAsync(
                experience.Id, includeIncoming: false, includeOutgoing: true);
            
            if (relationshipsResult.IsFailed)
            {
                _logger.LogError("Failed to get relationships for experience {ExperienceId}: {Errors}",
                    experience.Id, string.Join(", ", relationshipsResult.Errors.Select(e => e.Message)));
                return Result.Fail<List<Memorygram>>("Failed to get experience relationships");
            }

            // Filter for ROOT_OF relationships and get the target memorygrams
            var rootOfRelationships = relationshipsResult.Value
                .Where(r => r.RelationshipType == "ROOT_OF" && r.IsActive)
                .ToList();

            var chatMemorygrams = new List<Memorygram>();

            foreach (var relationship in rootOfRelationships)
            {
                var memorygramResult = await _memorygramService.GetMemorygramByIdAsync(relationship.ToMemorygramId);
                if (memorygramResult.IsSuccess)
                {
                    var memorygram = memorygramResult.Value;
                    // Only include UserInput and AssistantResponse in chat history
                    if (memorygram.Type == MemorygramType.UserInput ||
                        memorygram.Type == MemorygramType.AssistantResponse)
                    {
                        chatMemorygrams.Add(memorygram);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve memorygram {MemorygramId}: {Errors}",
                        relationship.ToMemorygramId, string.Join(", ", memorygramResult.Errors.Select(e => e.Message)));
                }
            }

            // Order by timestamp to maintain conversation sequence
            var orderedHistory = chatMemorygrams
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.CreatedAt)
                .ToList();

            _logger.LogInformation("Retrieved {Count} memorygrams for chat {ChatId}", orderedHistory.Count, chatId);
            return Result.Ok(orderedHistory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error retrieving chat history: {ex.Message}"));
        }
    }

    public async Task<Result<List<Memorygram>>> GetAllChatExperiencesAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all chat experiences");

            // Find all relationships of type HAS_CHAT_ID to identify chat experiences
            var chatRelationshipsResult = await _memorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID");
            if (chatRelationshipsResult.IsFailed)
            {
                _logger.LogError("Failed to get HAS_CHAT_ID relationships: {Errors}",
                    string.Join(", ", chatRelationshipsResult.Errors.Select(e => e.Message)));
                return Result.Fail<List<Memorygram>>("Failed to get chat relationships");
            }

            var chatExperiences = new List<Memorygram>();

            foreach (var relationship in chatRelationshipsResult.Value.Where(r => r.IsActive))
            {
                var experienceResult = await _memorygramService.GetMemorygramByIdAsync(relationship.FromMemorygramId);
                if (experienceResult.IsSuccess)
                {
                    var experience = experienceResult.Value;
                    // Verify this is an Experience memorygram with Chat subtype
                    if (experience.Type == MemorygramType.Experience &&
                        experience.Subtype != null)
                    {
                        chatExperiences.Add(experience);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve experience memorygram {MemorygramId}: {Errors}",
                        relationship.FromMemorygramId, string.Join(", ", experienceResult.Errors.Select(e => e.Message)));
                }
            }

            // Order by creation date (most recent first)
            var orderedExperiences = chatExperiences
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            _logger.LogInformation("Retrieved {Count} chat experiences", orderedExperiences.Count);
            return Result.Ok(orderedExperiences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all chat experiences");
            return Result.Fail(new Error($"Error retrieving chat experiences: {ex.Message}"));
        }
    }

    public async Task<Result<Memorygram?>> GetExperienceForChatAsync(Guid chatId)
    {
        try
        {
            _logger.LogInformation("Retrieving experience for chat {ChatId}", chatId);

            // Find relationships where the target is a chat metadata node with this chatId
            // We need to find HAS_CHAT_ID relationships where the Properties contain this chatId
            var chatRelationshipsResult = await _memorygramService.GetRelationshipsByTypeAsync("HAS_CHAT_ID");
            if (chatRelationshipsResult.IsFailed)
            {
                _logger.LogError("Failed to get HAS_CHAT_ID relationships: {Errors}",
                    string.Join(", ", chatRelationshipsResult.Errors.Select(e => e.Message)));
                return Result.Fail<Memorygram?>("Failed to get chat relationships");
            }

            // Look for a relationship that connects to this specific chatId
            // The chatId should be stored in the relationship properties or we can check the experience's Subtype
            foreach (var relationship in chatRelationshipsResult.Value.Where(r => r.IsActive))
            {
                var experienceResult = await _memorygramService.GetMemorygramByIdAsync(relationship.FromMemorygramId);
                if (experienceResult.IsSuccess)
                {
                    var experience = experienceResult.Value;
                    
                    // Check if this experience is for our chat (stored in Subtype)
                    if (experience.Type == MemorygramType.Experience &&
                        experience.Subtype == chatId.ToString())
                    {
                        _logger.LogInformation("Found experience {ExperienceId} for chat {ChatId}",
                            experience.Id, chatId);
                        return Result.Ok<Memorygram?>(experience);
                    }
                }
            }

            _logger.LogInformation("No experience found for chat {ChatId}", chatId);
            return Result.Ok<Memorygram?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving experience for chat {ChatId}", chatId);
            return Result.Fail(new Error($"Error retrieving experience for chat: {ex.Message}"));
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
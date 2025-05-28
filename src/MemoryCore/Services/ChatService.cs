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
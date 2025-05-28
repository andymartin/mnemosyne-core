using Microsoft.AspNetCore.SignalR;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Client;

namespace Mnemosyne.Core.Controllers;

public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendMessage(Guid chatId, string userText)
    {
        try
        {
            _logger.LogInformation("Received message for chat {ChatId} from connection {ConnectionId}",
                chatId, Context.ConnectionId);

            // Notify clients that processing has started
            await Clients.Caller.SendAsync("StatusUpdate", "Processing your message...");

            var result = await _chatService.ProcessUserMessageAsync(chatId, userText);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Message processed successfully for chat {ChatId}", chatId);
                
                // Create AssistantMessageDto with recalled memory system prompt
                var assistantMessageDto = new AssistantMessageDto
                {
                    MessageId = Guid.NewGuid(),
                    ConversationId = chatId,
                    Content = result.Value.Response,
                    IsComplete = true,
                    Timestamp = DateTime.UtcNow,
                    RecalledMemory = result.Value.SystemPrompt
                };
                
                // Send the assistant's response back to the client
                await Clients.Caller.SendAsync("ReceiveMessage", assistantMessageDto);
            }
            else
            {
                _logger.LogError("Failed to process message for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", result.Errors.Select(e => e.Message)));
                // Send error message back to the client
                await Clients.Caller.SendAsync("ErrorMessage",
                    "Failed to process your message: " + string.Join(", ", result.Errors.Select(e => e.Message)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message for chat {ChatId}", chatId);
            await Clients.Caller.SendAsync("ErrorMessage", "An unexpected error occurred while processing your message.");
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
using System;

namespace Mnemosyne.Core.Models.Client;

public class AssistantMessageDto
{
    public string MessageId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsComplete { get; set; } = true;
    public DateTime Timestamp { get; set; }
    public string? RecalledMemorySystemPrompt { get; set; }
}
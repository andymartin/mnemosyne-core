using System;

namespace Mnemosyne.Core.Models.Client;

public class AssistantMessageDto
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsComplete { get; set; } = true;
    public DateTime Timestamp { get; set; }
    public string? RecalledMemory { get; set; }
}
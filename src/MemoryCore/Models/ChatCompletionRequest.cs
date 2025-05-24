using System.Collections.Generic;

namespace Mnemosyne.Core.Models;

public class ChatCompletionRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public int? MaxTokens { get; set; }
    public float Temperature { get; set; } = 0.7f;
}
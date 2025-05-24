using System;

namespace Mnemosyne.Core.Models;

public class ChatCompletionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public ChatCompletionChoice[] Choices { get; set; } = Array.Empty<ChatCompletionChoice>();
    public ChatCompletionUsage Usage { get; set; } = new();
}
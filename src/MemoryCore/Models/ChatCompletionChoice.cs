using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Models;

public class ChatCompletionChoice
{
    public int Index { get; set; }
    public ChatMessage Message { get; set; } = new();
    public string FinishReason { get; set; } = string.Empty;
}
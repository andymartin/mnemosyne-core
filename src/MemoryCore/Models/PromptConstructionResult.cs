namespace Mnemosyne.Core.Models;

public class PromptConstructionResult
{
    public ChatCompletionRequest Request { get; set; } = new();
    public string SystemPrompt { get; set; } = string.Empty;
}
namespace Mnemosyne.Core.Models;

public class LanguageModelConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 4096;
    public string ModelName { get; set; } = string.Empty;
}
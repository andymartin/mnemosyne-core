namespace Mnemosyne.Core.Models.Pipelines;

public class ContextProvenance
{
    public const string ChatHistory = "Chat History";

    public DateTimeOffset Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string OriginalId { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

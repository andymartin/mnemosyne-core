namespace Mnemosyne.Core.Models.AWF;

public class ProcessingResult
{
    public string AssistantResponseText { get; set; } = string.Empty;
    public List<Guid> UtilizedMemoryIds { get; set; } = new();
}
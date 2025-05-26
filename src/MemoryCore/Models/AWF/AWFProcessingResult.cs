namespace Mnemosyne.Core.Models.AWF;

public class AWFProcessingResult
{
    public string AssistantResponseText { get; set; } = string.Empty;
    public ReflectionResult ReflectionData { get; set; } = new();
    public List<Guid> UtilizedMemoryIds { get; set; } = new();
}
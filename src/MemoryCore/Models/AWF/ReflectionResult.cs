namespace Mnemosyne.Core.Models.AWF;

public class ReflectionResult
{
    public string ReflectionNotes { get; set; } = string.Empty;
    public List<Guid> UtilizedMemoryIds { get; set; } = new();
    public Dictionary<string, object> AdditionalMetadata { get; set; } = new();
}
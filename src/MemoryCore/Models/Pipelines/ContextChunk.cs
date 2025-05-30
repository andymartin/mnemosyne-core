namespace Mnemosyne.Core.Models.Pipelines;

public class ContextChunk
{
    public MemorygramType Type { get; set; }
    public string? Subtype { get; set; } = string.Empty;
    public ContextProvenance Provenance { get; set; } = new();
    public float RelevanceScore { get; set; }
    public string Content { get; set; } = string.Empty;
}

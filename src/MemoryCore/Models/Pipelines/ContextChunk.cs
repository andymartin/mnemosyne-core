namespace Mnemosyne.Core.Models.Pipelines;

public class ContextChunk
{
    public ContextChunkType Type { get; set; }
    public ContextProvenance Provenance { get; set; } = new();
    public float RelevanceScore { get; set; }
    public string Content { get; set; } = string.Empty;
}

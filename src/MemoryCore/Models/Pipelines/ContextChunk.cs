namespace Mnemosyne.Core.Models.Pipelines;

public class ContextChunk
{
    public string SourcePipeline { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Provenance { get; set; } = string.Empty;
    public float RelevanceScore { get; set; }
    public string Content { get; set; } = string.Empty;
}

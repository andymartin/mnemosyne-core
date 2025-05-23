namespace Mnemosyne.Core.Models.Pipelines;

public class PipelineStageHistory
{
    public string StageName { get; set; } = string.Empty;
    public StageResult Result { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Message { get; set; }
}

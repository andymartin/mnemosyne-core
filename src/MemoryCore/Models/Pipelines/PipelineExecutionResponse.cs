namespace Mnemosyne.Core.Models.Pipelines;

public class PipelineExecutionResponse
{
    public Guid RunId { get; set; }
    public Guid PipelineId { get; set; }
    public string ResponseMessage { get; set; } = string.Empty;
    public object UpdatedMetadata { get; set; } = new();
    public PipelineExecutionState FinalState { get; set; } = new();
}
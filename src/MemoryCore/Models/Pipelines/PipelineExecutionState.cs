namespace Mnemosyne.Core.Models.Pipelines
{
    public class PipelineExecutionState
    {
        public Guid RunId { get; set; }
        public Guid PipelineId { get; set; }
        public PipelineExecutionRequest Request { get; set; } = new();
        public List<PipelineStageHistory> History { get; set; } = new();
        public List<ContextChunk> Context { get; set; } = new();
        public object? CurrentResult { get; set; }
    }
}
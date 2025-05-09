namespace Mnemosyne.Core.Models.Pipelines
{
    public class PipelineStageHistory
    {
        public string Name { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public string ExecutionLog { get; set; } = string.Empty;
    }
}

namespace Mnemosyne.Core.Models.Pipelines
{
    public class NullPipelineStage : PipelineStage
    {
        protected override async Task<PipelineExecutionResult> ExecuteInternalAsync(
            PipelineExecutionState state)
        {
            await Task.Delay(500);
            return new PipelineExecutionResult
            {
                ResponseMessage = "Simulated stage completed",
                UpdatedMetadata = new()
            };
        }
    }
}
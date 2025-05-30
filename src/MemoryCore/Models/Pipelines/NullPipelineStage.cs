namespace Mnemosyne.Core.Models.Pipelines;

public class NullPipelineStage : PipelineStage
{
    protected override Task<PipelineExecutionState> ExecuteInternalAsync(
        PipelineExecutionState state)
    {
        return Task.FromResult(state);
    }
}

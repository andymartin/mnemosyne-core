using Mnemosyne.Core.Interfaces;

namespace Mnemosyne.Core.Models.Pipelines;

public abstract class PipelineStage : IPipelineStage
{
    public string Name { get; init; }

    protected PipelineStage()
    {
        Name = GetType().Name;
    }

    public async Task<PipelineExecutionState> ExecuteAsync(
        PipelineExecutionState state,
        PipelineExecutionStatus status)
    {
        status.CurrentStageName = Name;
        status.CurrentStageStartTime = DateTimeOffset.UtcNow.DateTime;

        state = await ExecuteInternalAsync(state);

        return state;
    }

    protected abstract Task<PipelineExecutionState> ExecuteInternalAsync(
        PipelineExecutionState state);
}

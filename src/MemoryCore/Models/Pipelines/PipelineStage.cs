using Mnemosyne.Core.Interfaces;

namespace Mnemosyne.Core.Models.Pipelines
{
    public abstract class PipelineStage : IPipelineStage
    {
        public string Name { get; init; }

        protected PipelineStage()
        {
            Name = GetType().Name;
        }

        public async Task<PipelineExecutionResult> ExecuteAsync(
            PipelineExecutionState state,
            PipelineExecutionStatus status)
        {
            status.CurrentStageName = Name;
            status.CurrentStageStartTime = DateTimeOffset.UtcNow.DateTime;

            var result = await ExecuteInternalAsync(state);

            return result;
        }

        protected abstract Task<PipelineExecutionResult> ExecuteInternalAsync(
            PipelineExecutionState state);
    }
}

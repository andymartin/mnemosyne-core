namespace Mnemosyne.Core.Models.Pipelines;

public class NullPipelineStage : PipelineStage
{
    protected override async Task<PipelineExecutionState> ExecuteInternalAsync(
        PipelineExecutionState state)
    {
        await Task.Delay(500);

        // Add to context instead of creating a new result
        state.Context.Add(new ContextChunk
        {
            Type = ContextChunkType.Simulation,
            Provenance = new ContextProvenance
            {
                Source = "NullPipelineStage",
                Timestamp = DateTimeOffset.Now
            },
            Content = "Simulated stage completed"
        });

        return state;
    }
}
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces
{
    public interface IPipelineStage
    {
        string Name { get; }
        
        Task<PipelineExecutionState> ExecuteAsync(
            PipelineExecutionState state,
            PipelineExecutionStatus status);
    }
}

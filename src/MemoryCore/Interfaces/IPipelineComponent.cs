using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces
{
    public interface IPipelineComponent
    {
        string Name { get; }
        
        Task<PipelineExecutionResult> ExecuteAsync(
            PipelineExecutionState state,
            PipelineExecutionStatus status);
    }
}
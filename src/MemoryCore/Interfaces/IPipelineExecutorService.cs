using FluentResults;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces;

public interface IPipelineExecutorService
{
    Task<Result<PipelineExecutionState>> ExecutePipelineAsync(PipelineExecutionState state);
    Result<PipelineExecutionStatus> GetExecutionStatus(Guid runId);
}
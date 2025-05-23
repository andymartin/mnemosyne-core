using FluentResults;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces;

public interface IPipelineExecutorService
{
    Task<Result<PipelineExecutionState>> ExecutePipelineAsync(Guid pipelineId, PipelineExecutionRequest request);
    Result<PipelineExecutionStatus> GetExecutionStatus(Guid runId);
}
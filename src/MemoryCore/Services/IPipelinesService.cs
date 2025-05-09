using FluentResults;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Services;
public interface IPipelinesService
{
    Task<Result<PipelineManifest>> CreatePipelineAsync(PipelineManifest manifest);
    Task<Result> DeletePipelineAsync(Guid pipelineId);
    Task<Result<PipelineExecutionStatus>> ExecutePipelineAsync(Guid pipelineId, PipelineExecutionRequest request);
    Task<Result<IEnumerable<PipelineManifest>>> GetAllPipelinesAsync();
    Result<PipelineExecutionStatus> GetExecutionStatus(Guid runId);
    Task<Result<PipelineManifest>> GetPipelineAsync(Guid pipelineId);
    Task<Result<PipelineManifest>> UpdatePipelineAsync(Guid pipelineId, PipelineManifest manifest);
}

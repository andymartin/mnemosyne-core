using FluentResults;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces;

public interface IPipelinesService
{
    Task<Result<PipelineManifest>> CreatePipelineAsync(PipelineManifest manifest);
    Task<Result<PipelineManifest>> GetPipelineAsync(Guid pipelineId);
    Task<Result<IEnumerable<PipelineManifest>>> GetAllPipelinesAsync();
    Task<Result<PipelineManifest>> UpdatePipelineAsync(Guid pipelineId, PipelineManifest manifest);
    Task<Result> DeletePipelineAsync(Guid pipelineId);
}
using FluentResults;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces
{
    public interface IPipelinesRepository
    {
        Task<Result<PipelineManifest>> GetPipelineAsync(Guid pipelineId);
        Task<Result<IEnumerable<PipelineManifest>>> GetAllPipelinesAsync();
        Task<Result<PipelineManifest>> CreatePipelineAsync(PipelineManifest manifest); // Manifest itself is now the core definition entity
        Task<Result<PipelineManifest>> UpdatePipelineAsync(Guid pipelineId, PipelineManifest manifest);
        Task<Result> DeletePipelineAsync(Guid pipelineId); // Returns a simple Result for success/failure
    }
}
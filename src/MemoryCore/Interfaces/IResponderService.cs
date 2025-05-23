using FluentResults;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces;

public interface IResponderService
{
    Task<Result<string>> ProcessRequestAsync(PipelineExecutionRequest request);
}

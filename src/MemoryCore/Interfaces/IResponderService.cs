using FluentResults;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces;

public interface IResponderService
{
    Task<Result<ResponseResult>> ProcessRequestAsync(PipelineExecutionRequest request);
}

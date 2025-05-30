using FluentResults;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces;

public interface IPromptConstructor
{
    Result<PromptConstructionResult> ConstructPrompt(PipelineExecutionState state);
}
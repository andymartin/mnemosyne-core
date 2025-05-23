using FluentResults;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Interfaces;

public interface IPromptConstructor
{
    Result<string> ConstructPrompt(PipelineExecutionState state);
}
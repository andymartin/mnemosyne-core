using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Services;

public class PromptConstructor : IPromptConstructor
{
    public Result<string> ConstructPrompt(PipelineExecutionState state)
    {
        if (state?.Context == null || !state.Context.Any())
        {
            return Result.Fail<string>("Pipeline execution state contains no context to construct a prompt.");
        }

        var combinedPrompt = string.Join("\n", state.Context.Select(c => c.Content));

        if (string.IsNullOrWhiteSpace(combinedPrompt))
        {
            return Result.Fail<string>("Constructed prompt is empty or whitespace.");
        }

        return Result.Ok(combinedPrompt);
    }
}
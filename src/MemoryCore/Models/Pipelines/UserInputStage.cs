using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using System.Threading.Tasks;

namespace Mnemosyne.Core.Models.Pipelines;

public class UserInputStage : PipelineStage
{
    private readonly ILogger<UserInputStage> _logger;

    public UserInputStage(ILogger<UserInputStage> logger)
    {
        _logger = logger;
    }

    protected override Task<PipelineExecutionState> ExecuteInternalAsync(PipelineExecutionState state)
    {
        _logger.LogInformation("Executing UserInputStage for request: {UserInput}", state.Request.UserInput);

        if (string.IsNullOrWhiteSpace(state.Request.UserInput))
        {
            _logger.LogWarning("User input is empty or whitespace. Skipping UserInputStage.");
            return Task.FromResult(state);
        }

        state.Context.Add(new ContextChunk
        {
            Type = "UserInput",
            Content = state.Request.UserInput,
            Provenance = Name
        });

        return Task.FromResult(state);
    }
}
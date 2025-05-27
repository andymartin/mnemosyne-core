using FluentResults;
using Mnemosyne.Core.Interfaces;

namespace Mnemosyne.Core.Services;

public class ReflectiveResponder : IReflectiveResponder
{
    private readonly ILogger<ReflectiveResponder> _logger;

    public ReflectiveResponder(ILogger<ReflectiveResponder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ResponseEvaluation>> EvaluateResponseAsync(
        string userInput,
        string llmResponse)
    {
        _logger.LogInformation("Evaluating response for user input: {UserInput}", userInput);

        // MVP Placeholder: Always return true
        var evaluation = new ResponseEvaluation
        {
            ShouldDispatch = true,
            EvaluationNotes = "MVP placeholder - always dispatch response",
            Confidence = 1.0f
        };

        _logger.LogInformation("Response evaluation completed: ShouldDispatch={ShouldDispatch}",
            evaluation.ShouldDispatch);

        return await Task.FromResult(Result.Ok(evaluation));
    }
}
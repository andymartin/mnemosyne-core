using FluentResults;

namespace Mnemosyne.Core.Interfaces;

public interface IReflectiveResponder
{
    Task<Result<ResponseEvaluation>> EvaluateResponseAsync(string userInput, string llmResponse);
}

public class ResponseEvaluation
{
    public bool ShouldDispatch { get; set; }
    public string EvaluationNotes { get; set; } = string.Empty;
    public float Confidence { get; set; }
}
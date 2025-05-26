namespace Mnemosyne.Core.Models.AWF;

public class EvaluationResult
{
    public bool ShouldProceedToCpp { get; set; }
    public string EvaluationNotes { get; set; } = string.Empty;
}
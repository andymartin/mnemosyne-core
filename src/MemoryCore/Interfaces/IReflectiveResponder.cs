using FluentResults;
using Mnemosyne.Core.Models.AWF;

namespace Mnemosyne.Core.Interfaces;

public interface IReflectiveResponder
{
    Task<Result<EvaluationResult>> EvaluateContextAsync(PlanningContext context);
    Task<Result<ProcessingResult>> ProcessWithCppAsync(PlanningContext context);
    Task<Result<ReflectionResult>> PerformPostResponseAnalysisAsync(
        string userText,
        string assistantResponse,
        IEnumerable<Guid> utilizedMemoryIds);
}
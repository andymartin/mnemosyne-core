using FluentResults;
using Mnemosyne.Core.Models.AWF;

namespace Mnemosyne.Core.Interfaces;

public interface IResponsePlanner
{
    Task<Result<PlanningContext>> RetrieveAndPrepareContextAsync(
        string chatId,
        string userText,
        Guid userMemorygramId);
}
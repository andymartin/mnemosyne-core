using FluentResults;
using Mnemosyne.Core.Models.AWF;

namespace Mnemosyne.Core.Interfaces;

public interface IAgenticWorkflowService
{
    Task<Result<AWFProcessingResult>> ProcessMessageAsync(
        string chatId, 
        string userText, 
        Guid userMemorygramId);
}
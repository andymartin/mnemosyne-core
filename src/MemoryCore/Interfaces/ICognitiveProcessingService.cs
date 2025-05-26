using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface ICognitiveProcessingService
{
    Task<Result<CognitiveProcessingResult>> ProcessAsync(
        string userText,
        IEnumerable<Memorygram> threadHistory,
        IEnumerable<Memorygram> associativeMemories);
}

public class CognitiveProcessingResult
{
    public string ResponseText { get; set; } = string.Empty;
    public List<Guid> UtilizedMemoryIds { get; set; } = new();
}
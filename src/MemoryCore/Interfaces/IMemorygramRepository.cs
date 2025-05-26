using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface IMemorygramRepository
{
    Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram);
    Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight);
    Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id);

    /// <summary>
    /// Finds Memorygrams similar to the provided query vector
    /// </summary>
    /// <param name="queryVector">The vector embedding of the query</param>
    /// <param name="topK">The maximum number of results to return</param>
    /// <returns>A collection of Memorygrams with similarity scores</returns>
    Task<Result<IEnumerable<MemorygramWithScore>>> FindSimilarAsync(float[] queryVector, int topK);

    /// <summary>
    /// Retrieves all memorygrams for a specific chat ID
    /// </summary>
    /// <param name="chatId">The chat ID to retrieve memorygrams for</param>
    /// <returns>A collection of memorygrams for the specified chat</returns>
    Task<Result<IEnumerable<Memorygram>>> GetByChatIdAsync(string chatId);
}

using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

/// <summary>
/// Service interface for querying memory using vector similarity
/// </summary>
public interface IMemoryQueryService
{
    /// <summary>
    /// Queries the memory store for Memorygrams similar to the input query
    /// </summary>
    /// <param name="input">The query input containing the query text and optional parameters</param>
    /// <returns>A result containing the query results or error information</returns>
    Task<Result<MemoryQueryResult>> QueryAsync(MemoryQueryInput input);

    /// <summary>
    /// Retrieves chat history for a specific chat ID
    /// </summary>
    /// <param name="chatId">The chat ID to retrieve history for</param>
    /// <returns>A result containing the chat history memorygrams</returns>
    Task<Result<List<Memorygram>>> GetChatHistoryAsync(string chatId);

    /// <summary>
    /// Queries memory for similar memorygrams using text similarity
    /// </summary>
    /// <param name="queryText">The text to find similar memorygrams for</param>
    /// <param name="topK">Number of top results to return (default: 5)</param>
    /// <returns>A result containing similar memorygrams</returns>
    Task<Result<List<MemorygramWithScore>>> QueryMemoryAsync(string queryText, int topK = 5);
}
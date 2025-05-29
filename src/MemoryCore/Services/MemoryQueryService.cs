using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Microsoft.Extensions.Logging;

namespace Mnemosyne.Core.Services;

/// <summary>
/// Service for querying memory using vector similarity
/// </summary>
public class MemoryQueryService : IMemoryQueryService
{
    private readonly ISemanticReformulator _semanticReformulator;
    private readonly IEmbeddingService _embeddingService;
    private readonly IMemorygramRepository _memorygramRepository;
    private readonly ILogger<MemoryQueryService> _logger;
    private const int DefaultTopK = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryQueryService"/> class.
    /// </summary>
    /// <param name="semanticReformulator">The semantic reformulator</param>
    /// <param name="embeddingService">The embedding service</param>
    /// <param name="memorygramRepository">The memorygram repository</param>
    /// <param name="logger">The logger</param>
    public MemoryQueryService(
        ISemanticReformulator semanticReformulator,
        IEmbeddingService embeddingService,
        IMemorygramRepository memorygramRepository,
        ILogger<MemoryQueryService> logger)
    {
        _semanticReformulator = semanticReformulator ?? throw new ArgumentNullException(nameof(semanticReformulator));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _memorygramRepository = memorygramRepository ?? throw new ArgumentNullException(nameof(memorygramRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Queries the memory store for Memorygrams similar to the input query
    /// </summary>
    /// <param name="input">The query input containing the query text and optional parameters</param>
    /// <returns>A result containing the query results or error information</returns>
    public async Task<Result<MemoryQueryResult>> QueryAsync(MemoryQueryInput input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input.QueryText))
            {
                return Result.Fail(new Error("Query text cannot be empty"));
            }

            int topK = input.TopK ?? DefaultTopK;
            if (topK <= 0)
            {
                return Result.Fail(new Error("TopK must be greater than 0"));
            }

            string queryText = input.QueryText;
            Guid? excludeChatId = input.ExcludeChatId;

            Result<MemoryReformulations> reformulationsResult = await _semanticReformulator.ReformulateForQueryAsync(queryText);

            if (reformulationsResult.IsFailed)
            {
                _logger.LogError("Failed to reformulate query: {Errors}", string.Join(", ", reformulationsResult.Errors.Select(e => e.Message)));
                return Result.Fail(new Error("Failed to reformulate query."));
            }

            MemoryReformulations reformulations = reformulationsResult.Value;
            List<MemorygramWithScore> allResults = new List<MemorygramWithScore>();

            foreach (MemoryReformulationType type in Enum.GetValues(typeof(MemoryReformulationType)))
            {
                string? reformulatedQueryText = reformulations[type];
                if (!string.IsNullOrEmpty(reformulatedQueryText))
                {
                    Result<float[]> embeddingResult = await _embeddingService.GetEmbeddingAsync(reformulatedQueryText);
                    if (embeddingResult.IsSuccess)
                    {
                        Result<IEnumerable<MemorygramWithScore>> repoResult = await _memorygramRepository.FindSimilarAsync(
                            embeddingResult.Value,
                            type,
                            topK,
                            excludeChatId
                        );
                        if (repoResult.IsSuccess && repoResult.Value != null)
                        {
                            allResults.AddRange(repoResult.Value);
                        }
                        else
                        {
                            _logger.LogWarning("Repository query failed for reformulation type {Type}: {Errors}", type, string.Join(", ", repoResult.Errors.Select(e => e.Message)));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Embedding generation failed for reformulation type {Type}: {Errors}", type, string.Join(", ", embeddingResult.Errors.Select(e => e.Message)));
                    }
                }
            }

            var groupedResults = allResults.GroupBy(r => r.Id);
            var distinctBestResults = groupedResults.Select(g => g.OrderByDescending(r => r.Score).First()).ToList();
            var finalRankedResults = distinctBestResults.OrderByDescending(r => r.Score).Take(topK).ToList();

            List<MemorygramResultItem> resultItems = finalRankedResults
                .Select(m => new MemorygramResultItem(
                    m.Id,
                    m.Content,
                    m.Score,
                    m.CreatedAt,
                    m.UpdatedAt))
                .ToList();

            return Result.Ok(new MemoryQueryResult("success", resultItems, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing memory query");
            return Result.Fail(new Error($"Error executing memory query: {ex.Message}"));
        }
    }

    /// <summary>
    /// Retrieves chat history for a specific chat ID
    /// </summary>
    /// <param name="chatId">The chat ID to retrieve history for</param>
    /// <returns>A result containing the chat history memorygrams</returns>
    public async Task<Result<List<Memorygram>>> GetChatHistoryAsync(string chatId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(chatId))
            {
                return Result.Fail(new Error("Chat ID cannot be empty"));
            }

            _logger.LogInformation("Retrieving chat history for chat ID: {ChatId}", chatId);

            // Get memorygrams by chat ID from repository
            var result = await _memorygramRepository.GetByChatIdAsync(chatId);
            
            if (result.IsFailed)
            {
                string errorMessage = string.Join(", ", result.Errors.Select(e => e.Message));
                _logger.LogError("Failed to retrieve chat history: {ErrorMessage}", errorMessage);
                return Result.Fail(new Error($"Failed to retrieve chat history: {errorMessage}"));
            }

            var memorygrams = result.Value.ToList();
            _logger.LogInformation("Retrieved {Count} memorygrams for chat {ChatId}", memorygrams.Count, chatId);

            return Result.Ok(memorygrams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history for chat ID: {ChatId}", chatId);
            return Result.Fail(new Error($"Error retrieving chat history: {ex.Message}"));
        }
    }

    /// <summary>
    /// Queries memory for similar memorygrams using text similarity
    /// </summary>
    /// <param name="queryText">The text to find similar memorygrams for</param>
    /// <param name="topK">Number of top results to return (default: 5)</param>
    /// <param name="excludeChatId">Optional chat ID to exclude memorygrams from</param>
    /// <returns>A result containing similar memorygrams</returns>
    public async Task<Result<List<MemorygramWithScore>>> QueryMemoryAsync(string queryText, int topK = 5, Guid? excludeChatId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return Result.Fail(new Error("Query text cannot be empty"));
            }

            if (topK <= 0)
            {
                return Result.Fail(new Error("TopK must be greater than 0"));
            }

            List<MemorygramWithScore> allResults = new List<MemorygramWithScore>();

            Result<MemoryReformulations> reformulationsResult = await _semanticReformulator.ReformulateForQueryAsync(queryText);

            if (reformulationsResult.IsFailed)
            {
                _logger.LogError("Failed to reformulate query for QueryMemoryAsync: {Errors}", string.Join(", ", reformulationsResult.Errors.Select(e => e.Message)));
                return Result.Fail(new Error("Failed to reformulate query for QueryMemoryAsync."));
            }

            MemoryReformulations reformulations = reformulationsResult.Value;

            foreach (MemoryReformulationType type in Enum.GetValues(typeof(MemoryReformulationType)))
            {
                string? reformulatedQueryText = reformulations[type];
                if (!string.IsNullOrEmpty(reformulatedQueryText))
                {
                    Result<float[]> embeddingResult = await _embeddingService.GetEmbeddingAsync(reformulatedQueryText);
                    if (embeddingResult.IsSuccess)
                    {
                        Result<IEnumerable<MemorygramWithScore>> repoResult = await _memorygramRepository.FindSimilarAsync(
                            embeddingResult.Value,
                            type,
                            topK,
                            excludeChatId
                        );
                        if (repoResult.IsSuccess && repoResult.Value != null)
                        {
                            allResults.AddRange(repoResult.Value);
                        }
                        else
                        {
                            _logger.LogWarning("Repository query failed for reformulation type {Type} in QueryMemoryAsync: {Errors}", type, string.Join(", ", repoResult.Errors.Select(e => e.Message)));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Embedding generation failed for reformulation type {Type} in QueryMemoryAsync: {Errors}", type, string.Join(", ", embeddingResult.Errors.Select(e => e.Message)));
                    }
                }
            }

            var groupedResults = allResults.GroupBy(r => r.Id);
            var distinctBestResults = groupedResults.Select(g => g.OrderByDescending(r => r.Score).First()).ToList();
            var finalRankedResults = distinctBestResults.OrderByDescending(r => r.Score).Take(topK).ToList();

            return Result.Ok(finalRankedResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing memory query for text: {QueryText}", queryText);
            return Result.Fail(new Error($"Error executing memory query: {ex.Message}"));
        }
    }
}

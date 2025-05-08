using FluentResults;
using MemoryCore.Interfaces;
using MemoryCore.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MemoryCore.Services
{
    /// <summary>
    /// Service for querying memory using vector similarity
    /// </summary>
    public class MemoryQueryService : IMemoryQueryService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IMemorygramRepository _memorygramRepository;
        private readonly ILogger<MemoryQueryService> _logger;
        private const int DefaultTopK = 5;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryQueryService"/> class.
        /// </summary>
        /// <param name="embeddingService">The embedding service</param>
        /// <param name="memorygramRepository">The memorygram repository</param>
        /// <param name="logger">The logger</param>
        public MemoryQueryService(
            IEmbeddingService embeddingService,
            IMemorygramRepository memorygramRepository,
            ILogger<MemoryQueryService> logger)
        {
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _memorygramRepository = memorygramRepository ?? throw new ArgumentNullException(nameof(memorygramRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Queries the memory store for Memorygrams similar to the input query
        /// </summary>
        /// <param name="input">The query input containing the query text and optional parameters</param>
        /// <returns>A result containing the query results or error information</returns>
        public async Task<Result<McpQueryResult>> QueryAsync(McpQueryInput input)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(input.QueryText))
                {
                    return Result.Fail(new Error("Query text cannot be empty"));
                }

                int topK = input.TopK ?? DefaultTopK;
                if (topK <= 0)
                {
                    return Result.Fail(new Error("TopK must be greater than 0"));
                }

                // Get embedding for query text
                _logger.LogInformation("Getting embedding for query text: {QueryText}", input.QueryText);
                Result<float[]> embeddingResult = await _embeddingService.GetEmbeddingAsync(input.QueryText);

                if (embeddingResult.IsFailed)
                {
                    string errorMessage = string.Join(", ", embeddingResult.Errors.Select(e => e.Message));
                    _logger.LogError("Failed to get embedding: {ErrorMessage}", errorMessage);
                    return Result.Fail(new Error($"Failed to get embedding: {errorMessage}"));
                }

                float[] queryVector = embeddingResult.Value;

                // Find similar memorygrams
                _logger.LogInformation("Finding similar memorygrams with topK: {TopK}", topK);
                Result<IEnumerable<MemorygramWithScore>> similarResult = 
                    await _memorygramRepository.FindSimilarAsync(queryVector, topK);

                if (similarResult.IsFailed)
                {
                    string errorMessage = string.Join(", ", similarResult.Errors.Select(e => e.Message));
                    _logger.LogError("Failed to find similar memorygrams: {ErrorMessage}", errorMessage);
                    return Result.Fail(new Error($"Failed to find similar memorygrams: {errorMessage}"));
                }

                IEnumerable<MemorygramWithScore> similarMemorygrams = similarResult.Value;

                // Convert to result items
                List<MemorygramResultItem> resultItems = similarMemorygrams
                    .Select(m => new MemorygramResultItem(
                        m.Id,
                        m.Content,
                        m.Score,
                        m.CreatedAt,
                        m.UpdatedAt))
                    .ToList();

                _logger.LogInformation("Query returned {Count} results", resultItems.Count);

                // Return success result
                return Result.Ok(new McpQueryResult("success", resultItems, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing memory query");
                return Result.Fail(new Error($"Error executing memory query: {ex.Message}"));
            }
        }
    }
}
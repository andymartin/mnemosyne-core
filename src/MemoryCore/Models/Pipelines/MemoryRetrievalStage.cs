using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Models.Pipelines;

public class MemoryRetrievalStage : PipelineStage
{
    private const int MaxChunksToInclude = 5;
    
    private readonly IMemoryQueryService _memoryQueryService;
    private readonly ILogger<MemoryRetrievalStage> _logger;
    private readonly float? _minimumSimilarityScore;

    public MemoryRetrievalStage(
        IMemoryQueryService memoryQueryService,
        ILogger<MemoryRetrievalStage> logger,
        float? minimumSimilarityScore = null)
    {
        _memoryQueryService = memoryQueryService;
        _logger = logger;
        _minimumSimilarityScore = minimumSimilarityScore;
    }

    protected override async Task<PipelineExecutionState> ExecuteInternalAsync(PipelineExecutionState state)
    {
        try
        {
            var userInput = state.Request.UserInput;
            
            if (string.IsNullOrWhiteSpace(userInput))
            {
                _logger.LogWarning("User input is null or empty, skipping memory retrieval");
                return state;
            }

            _logger.LogInformation("Starting memory retrieval for user input: {UserInput}", userInput);

            // Retrieve memories based on user input
            var queryResult = await _memoryQueryService.QueryMemoryAsync(userInput, MaxChunksToInclude);
            
            if (queryResult.IsFailed)
            {
                _logger.LogError("Failed to query memory: {Errors}", string.Join(", ", queryResult.Errors.Select(e => e.Message)));
                return state;
            }

            var memorygrams = queryResult.Value;
            _logger.LogInformation("Retrieved {Count} memorygrams from memory query", memorygrams.Count);

            // Filter by minimum similarity score if configured
            if (_minimumSimilarityScore.HasValue)
            {
                memorygrams = memorygrams
                    .Where(m => m.Score >= _minimumSimilarityScore.Value)
                    .ToList();
                
                _logger.LogInformation("After filtering by minimum similarity score {MinScore}, {Count} memorygrams remain", 
                    _minimumSimilarityScore.Value, memorygrams.Count);
            }

            // Convert memorygrams to context chunks
            foreach (var memorygram in memorygrams)
            {
                var contextChunk = new ContextChunk
                {
                    Type = ContextChunkType.Memory,
                    Content = memorygram.Content,
                    RelevanceScore = memorygram.Score,
                    Provenance = new ContextProvenance
                    {
                        Source = "MemoryRetrievalStage",
                        OriginalId = memorygram.Id.ToString(),
                        Timestamp = memorygram.UpdatedAt,
                        Metadata = new Dictionary<string, object>
                        {
                            { "MemorygramSource", memorygram.Source ?? string.Empty },
                            { "MemorygramType", memorygram.Type.ToString() }
                        }
                    }
                };

                state.Context.Add(contextChunk);
                _logger.LogDebug("Added context chunk from memorygram {Id} with score {Score}", 
                    memorygram.Id, memorygram.Score);
            }

            _logger.LogInformation("Memory retrieval completed successfully. Added {Count} context chunks", memorygrams.Count);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during memory retrieval");
            return state;
        }
    }
}
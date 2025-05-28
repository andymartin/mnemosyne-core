using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public class MemorygramService : IMemorygramService
{
    private readonly IMemorygramRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISemanticReformulator _semanticReformulator;
    private readonly ILogger<MemorygramService> _logger;

    public MemorygramService(
        IMemorygramRepository repository,
        IEmbeddingService embeddingService,
        ISemanticReformulator semanticReformulator,
        ILogger<MemorygramService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _semanticReformulator = semanticReformulator ?? throw new ArgumentNullException(nameof(semanticReformulator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram)
    {
        try
        {
            string rawContent = memorygram.Content;
            Result<MemoryReformulations> reformulationsResult = await _semanticReformulator.ReformulateForStorageAsync(rawContent);

            if (reformulationsResult.IsFailed)
            {
                _logger.LogError("Failed to reformulate content for memorygram {MemorygramId}: {Errors}", memorygram.Id, reformulationsResult.Errors);
                return Result.Fail<Memorygram>(reformulationsResult.Errors);
            }

            MemoryReformulations reformulations = reformulationsResult.Value;
            memorygram = await PopulateEmbeddingsAsync(memorygram, reformulations);
            
            return await _repository.CreateOrUpdateMemorygramAsync(memorygram);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or updating memorygram {MemorygramId}", memorygram.Id);
            return Result.Fail<Memorygram>($"Service error: {ex.Message}");
        }
    }

    private async Task<Memorygram> PopulateEmbeddingsAsync(Memorygram memorygram, MemoryReformulations reformulations)
    {
        foreach (MemoryReformulationType type in Enum.GetValues(typeof(MemoryReformulationType)))
        {
            string? reformulatedText = reformulations[type];
            if (!string.IsNullOrEmpty(reformulatedText))
            {
                Result<float[]> embeddingResult = await _embeddingService.GetEmbeddingAsync(reformulatedText);
                if (embeddingResult.IsSuccess)
                {
                    memorygram = type switch
                    {
                        MemoryReformulationType.Topical => memorygram with { TopicalEmbedding = embeddingResult.Value },
                        MemoryReformulationType.Content => memorygram with { ContentEmbedding = embeddingResult.Value },
                        MemoryReformulationType.Context => memorygram with { ContextEmbedding = embeddingResult.Value },
                        MemoryReformulationType.Metadata => memorygram with { MetadataEmbedding = embeddingResult.Value },
                        _ => memorygram
                    };
                }
                else
                {
                    _logger.LogWarning("Failed to generate embedding for {ReformulationType} of memorygram {MemorygramId}: {Errors}", type, memorygram.Id, embeddingResult.Errors);
                }
            }
        }
        return memorygram;
    }

    public async Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight)
    {
        try
        {
            return await _repository.CreateAssociationAsync(fromId, toId, weight);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message creating association between {FromId} and {ToId}", fromId, toId);
            return Result.Fail($"Service error: {ex.Message}");
        }
    }

    public async Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id)
    {
        try
        {
            return await _repository.GetMemorygramByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message retrieving memorygram by ID {Id}", id);
            return Result.Fail<Memorygram>($"Service error: {ex.Message}");
        }
    }
}

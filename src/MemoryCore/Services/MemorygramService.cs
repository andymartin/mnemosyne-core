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

    public async Task<Result<GraphRelationship>> CreateRelationshipAsync(Guid fromId, Guid toId, string relationshipType, float weight, string? properties = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relationshipType))
            {
                return Result.Fail<GraphRelationship>("Relationship type cannot be null or empty");
            }

            if (weight < 0 || weight > 1)
            {
                return Result.Fail<GraphRelationship>("Weight must be between 0 and 1");
            }

            return await _repository.CreateRelationshipAsync(fromId, toId, relationshipType, weight, properties);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relationship between {FromId} and {ToId}", fromId, toId);
            return Result.Fail<GraphRelationship>($"Service error: {ex.Message}");
        }
    }

    public async Task<Result<GraphRelationship>> UpdateRelationshipAsync(Guid relationshipId, float? weight = null, string? properties = null, bool? isActive = null)
    {
        try
        {
            if (weight.HasValue && (weight < 0 || weight > 1))
            {
                return Result.Fail<GraphRelationship>("Weight must be between 0 and 1");
            }

            return await _repository.UpdateRelationshipAsync(relationshipId, weight, properties, isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating relationship {RelationshipId}", relationshipId);
            return Result.Fail<GraphRelationship>($"Service error: {ex.Message}");
        }
    }

    public async Task<Result> DeleteRelationshipAsync(Guid relationshipId)
    {
        try
        {
            return await _repository.DeleteRelationshipAsync(relationshipId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting relationship {RelationshipId}", relationshipId);
            return Result.Fail($"Service error: {ex.Message}");
        }
    }

    public async Task<Result<GraphRelationship>> GetRelationshipByIdAsync(Guid relationshipId)
    {
        try
        {
            return await _repository.GetRelationshipByIdAsync(relationshipId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relationship {RelationshipId}", relationshipId);
            return Result.Fail<GraphRelationship>($"Service error: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByMemorygramIdAsync(Guid memorygramId, bool includeIncoming = true, bool includeOutgoing = true)
    {
        try
        {
            return await _repository.GetRelationshipsByMemorygramIdAsync(memorygramId, includeIncoming, includeOutgoing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relationships for memorygram {MemorygramId}", memorygramId);
            return Result.Fail<IEnumerable<GraphRelationship>>($"Service error: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByTypeAsync(string relationshipType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relationshipType))
            {
                return Result.Fail<IEnumerable<GraphRelationship>>("Relationship type cannot be null or empty");
            }

            return await _repository.GetRelationshipsByTypeAsync(relationshipType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relationships of type {RelationshipType}", relationshipType);
            return Result.Fail<IEnumerable<GraphRelationship>>($"Service error: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<GraphRelationship>>> FindRelationshipsAsync(Guid? fromId = null, Guid? toId = null, string? relationshipType = null, float? minWeight = null, float? maxWeight = null, bool? isActive = null)
    {
        try
        {
            if (minWeight.HasValue && (minWeight < 0 || minWeight > 1))
            {
                return Result.Fail<IEnumerable<GraphRelationship>>("Minimum weight must be between 0 and 1");
            }

            if (maxWeight.HasValue && (maxWeight < 0 || maxWeight > 1))
            {
                return Result.Fail<IEnumerable<GraphRelationship>>("Maximum weight must be between 0 and 1");
            }

            if (minWeight.HasValue && maxWeight.HasValue && minWeight > maxWeight)
            {
                return Result.Fail<IEnumerable<GraphRelationship>>("Minimum weight cannot be greater than maximum weight");
            }

            return await _repository.FindRelationshipsAsync(fromId, toId, relationshipType, minWeight, maxWeight, isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding relationships with specified criteria");
            return Result.Fail<IEnumerable<GraphRelationship>>($"Service error: {ex.Message}");
        }
    }
}

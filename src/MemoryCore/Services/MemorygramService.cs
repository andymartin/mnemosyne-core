using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public class MemorygramService : IMemorygramService
{
    private readonly IMemorygramRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<MemorygramService> _logger;

    public MemorygramService(
        IMemorygramRepository repository,
        IEmbeddingService embeddingService,
        ILogger<MemorygramService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram)
    {
        try
        {
            // Generate embedding for the memorygram content
            var embeddingResult = await _embeddingService.GetEmbeddingAsync(memorygram.Content);

            if (embeddingResult.IsFailed)
            {
                _logger.LogError("Failed to generate embedding: {Errors}", string.Join(", ", embeddingResult.Errors));
                return Result.Fail<Memorygram>(embeddingResult.Errors);
            }

            // Create a new memorygram with the embeddings
            // Note: Currently using the same embedding for all four embedding types
            // This will be updated in future epics with specialized embedding generation
            var memorgramWithEmbedding = memorygram with 
            { 
                TopicalEmbedding = embeddingResult.Value,
                ContentEmbedding = embeddingResult.Value,
                ContextEmbedding = embeddingResult.Value,
                MetadataEmbedding = embeddingResult.Value
            };

            // Store the memorygram with its embeddings
            return await _repository.CreateOrUpdateMemorygramAsync(memorgramWithEmbedding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message creating or updating memorygram");
            return Result.Fail<Memorygram>($"Service error: {ex.Message}");
        }
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

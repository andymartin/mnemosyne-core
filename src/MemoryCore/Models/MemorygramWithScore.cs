namespace Mnemosyne.Core.Models;

/// <summary>
/// Represents a Memorygram with a similarity score from vector search
/// </summary>
public record MemorygramWithScore(
    Guid Id,
    string Content,
    MemorygramType Type,
    float[] TopicalEmbedding,
    float[] ContentEmbedding,
    float[] ContextEmbedding,
    float[] MetadataEmbedding,
    string Source,
    long Timestamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? ChatId,
    Guid? PreviousMemorygramId,
    Guid? NextMemorygramId,
    int? Sequence,
    float Score
);
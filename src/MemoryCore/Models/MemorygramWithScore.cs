namespace Mnemosyne.Core.Models;

/// <summary>
/// Represents a Memorygram with a similarity score from vector search
/// </summary>
public record MemorygramWithScore(
    Guid Id,
    string Content,
    MemorygramType Type,
    float[] VectorEmbedding,
    string Source,
    long Timestamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ChatId,
    Guid? PreviousMemorygramId,
    Guid? NextMemorygramId,
    int? Sequence,
    float Score
);
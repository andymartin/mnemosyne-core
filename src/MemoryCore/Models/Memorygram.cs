namespace Mnemosyne.Core.Models;

public record Memorygram(
    Guid Id,
    string Content,
    MemorygramType Type,
    float[] VectorEmbedding,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
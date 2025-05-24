using System;

namespace Mnemosyne.Core.Models;

public record Memorygram(
    Guid Id,
    string Content,
    MemorygramType Type,
    float[] VectorEmbedding,
    string Source,
    long Timestamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
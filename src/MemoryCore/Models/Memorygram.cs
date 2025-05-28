using System;

namespace Mnemosyne.Core.Models;

public record Memorygram(
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
    string? ChatId = null,
    Guid? PreviousMemorygramId = null,
    Guid? NextMemorygramId = null,
    int? Sequence = null
);
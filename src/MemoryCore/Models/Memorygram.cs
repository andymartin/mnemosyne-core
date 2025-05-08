using System;

namespace MemoryCore.Models
{
    public record Memorygram(
        Guid Id,
        string Content,
        float[] VectorEmbedding,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );
}
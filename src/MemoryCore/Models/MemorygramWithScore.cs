using System;
using System.Collections.Generic;

namespace MemoryCore.Models
{
    /// <summary>
    /// Represents a Memorygram with a similarity score from vector search
    /// </summary>
    public record MemorygramWithScore(
        Guid Id,
        string Content,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        float Score
    );
}
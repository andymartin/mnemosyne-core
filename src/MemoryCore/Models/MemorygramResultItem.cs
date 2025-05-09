namespace Mnemosyne.Core.Models;

/// <summary>
/// Represents a Memorygram item in the query results
/// </summary>
public record MemorygramResultItem(
    Guid Id,
    string Content,
    float Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
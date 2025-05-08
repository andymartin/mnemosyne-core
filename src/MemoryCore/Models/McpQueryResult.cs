using System;
using System.Collections.Generic;

namespace MemoryCore.Models
{
    /// <summary>
    /// Result model for the queryMemory MCP tool
    /// </summary>
    public record McpQueryResult(string Status, List<MemorygramResultItem>? Results, string? Message);

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
}
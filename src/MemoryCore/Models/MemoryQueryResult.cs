namespace Mnemosyne.Core.Models;

/// <summary>
/// Result model for the queryMemory MCP tool
/// </summary>
public record MemoryQueryResult(string Status, List<MemorygramResultItem>? Results, string? Message);

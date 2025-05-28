namespace Mnemosyne.Core.Models;

/// <summary>
/// Input model for the queryMemory MCP tool
/// </summary>
public record MemoryQueryInput(string QueryText, int? TopK);

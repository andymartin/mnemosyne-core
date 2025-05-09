using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Mcp;

public interface IQueryMemoryTool
{
    /// <summary>
    /// Queries the memory store for Memorygrams similar to the input query
    /// </summary>
    /// <param name="input">The query input containing the query text and optional parameters</param>
    /// <returns>A result containing the query results or error information</returns>
    Task<McpQueryResult> QueryMemoryAsync(McpQueryInput input);
}

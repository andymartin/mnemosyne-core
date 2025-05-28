using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using ModelContextProtocol.Server;

namespace Mnemosyne.Core.Mcp;

/// <summary>
/// MCP tool for querying memory using vector similarity
/// </summary>
[McpServerToolType]
public class QueryMemoryTool : IQueryMemoryTool
{
    private readonly IMemoryQueryService _memoryQueryService;
    private readonly ILogger<QueryMemoryTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMemoryTool"/> class.
    /// </summary>
    /// <param name="memoryQueryService">The memory query service</param>
    /// <param name="logger">The logger</param>
    public QueryMemoryTool(IMemoryQueryService memoryQueryService, ILogger<QueryMemoryTool> logger)
    {
        _memoryQueryService = memoryQueryService ?? throw new ArgumentNullException(nameof(memoryQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Queries the memory store for Memorygrams similar to the input query
    /// </summary>
    /// <param name="input">The query input containing the query text and optional parameters</param>
    /// <returns>A result containing the query results or error information</returns>
    [McpServerTool(Name = "queryMemory")]
    public async Task<MemoryQueryResult> QueryMemoryAsync(MemoryQueryInput input)
    {
        if (input == null)
        {
            _logger.LogError("QueryMemoryAsync called with null input");
            return new MemoryQueryResult("error", null, "Query input cannot be null");
        }

        if (string.IsNullOrWhiteSpace(input.QueryText))
        {
            _logger.LogError("QueryMemoryAsync called with empty query text");
            return new MemoryQueryResult("error", null, "Query text cannot be empty");
        }

        _logger.LogInformation("Executing memory query with text: {QueryText}, TopK: {TopK}",
            input.QueryText, input.TopK);

        Result<MemoryQueryResult> result = await _memoryQueryService.QueryAsync(input);

        if (result.IsSuccess)
        {
            return result.Value;
        }
        else
        {
            string errorMessage = string.Join(", ", result.Errors.Select(e => e.Message));
            _logger.LogError("Message executing memory query: {ErrorMessage}", errorMessage);
            return new MemoryQueryResult("error", null, errorMessage);
        }
    }
}

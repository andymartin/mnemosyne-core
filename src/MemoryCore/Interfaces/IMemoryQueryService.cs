using FluentResults;
using MemoryCore.Models;
using System.Threading.Tasks;

namespace MemoryCore.Interfaces
{
    /// <summary>
    /// Service interface for querying memory using vector similarity
    /// </summary>
    public interface IMemoryQueryService
    {
        /// <summary>
        /// Queries the memory store for Memorygrams similar to the input query
        /// </summary>
        /// <param name="input">The query input containing the query text and optional parameters</param>
        /// <returns>A result containing the query results or error information</returns>
        Task<Result<McpQueryResult>> QueryAsync(McpQueryInput input);
    }
}
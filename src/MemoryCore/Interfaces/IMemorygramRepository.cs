using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface IMemorygramRepository
{
    Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram);
    Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight);
    Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id);

    /// <summary>
    /// Finds Memorygrams similar to the provided query vector
    /// </summary>
    /// <param name="queryVector">The vector embedding of the query</param>
    /// <param name="reformulationType">The type of reformulation to use for the query (e.g., Topical, Content)</param>
    /// <param name="topK">The maximum number of results to return</param>
    /// <param name="excludeSubtype">Optional subtype to exclude memorygrams from</param>
    /// <returns>A collection of Memorygrams with similarity scores</returns>
    Task<Result<IEnumerable<MemorygramWithScore>>> FindSimilarAsync(
        float[] queryVector,
        MemoryReformulationType reformulationType,
        int topK,
        string? excludeSubtype = null
    );

    /// <summary>
    /// Retrieves all memorygrams for a specific subtype
    /// </summary>
    /// <param name="subtype">The subtype to retrieve memorygrams for</param>
    /// <returns>A collection of memorygrams for the specified subtype</returns>
    Task<Result<IEnumerable<Memorygram>>> GetBySubtypeAsync(string subtype);

    /// <summary>
    /// Retrieves all chat initiation messages (memorygrams where PreviousMemorygramId is null and Subtype is not null)
    /// </summary>
    /// <returns>A collection of memorygrams representing chat initiations</returns>
    Task<Result<IEnumerable<Memorygram>>> GetAllChatsAsync();

    Task<Result<GraphRelationship>> CreateRelationshipAsync(Guid fromId, Guid toId, string relationshipType, float weight, string? properties = null);
    Task<Result<GraphRelationship>> UpdateRelationshipAsync(Guid relationshipId, float? weight = null, string? properties = null, bool? isActive = null);
    Task<Result> DeleteRelationshipAsync(Guid relationshipId);
    Task<Result<GraphRelationship>> GetRelationshipByIdAsync(Guid relationshipId);
    Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByMemorygramIdAsync(Guid memorygramId, bool includeIncoming = true, bool includeOutgoing = true);
    Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByTypeAsync(string relationshipType);
    Task<Result<IEnumerable<GraphRelationship>>> FindRelationshipsAsync(Guid? fromId = null, Guid? toId = null, string? relationshipType = null, float? minWeight = null, float? maxWeight = null, bool? isActive = null);
}

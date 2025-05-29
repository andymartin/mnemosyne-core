using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public interface IMemorygramService
{
    Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram);
    Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight);
    Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id);
    
    Task<Result<GraphRelationship>> CreateRelationshipAsync(Guid fromId, Guid toId, string relationshipType, float weight, string? properties = null);
    Task<Result<GraphRelationship>> UpdateRelationshipAsync(Guid relationshipId, float? weight = null, string? properties = null, bool? isActive = null);
    Task<Result> DeleteRelationshipAsync(Guid relationshipId);
    Task<Result<GraphRelationship>> GetRelationshipByIdAsync(Guid relationshipId);
    Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByMemorygramIdAsync(Guid memorygramId, bool includeIncoming = true, bool includeOutgoing = true);
    Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByTypeAsync(string relationshipType);
    Task<Result<IEnumerable<GraphRelationship>>> FindRelationshipsAsync(Guid? fromId = null, Guid? toId = null, string? relationshipType = null, float? minWeight = null, float? maxWeight = null, bool? isActive = null);
}
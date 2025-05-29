using System;

namespace Mnemosyne.Core.Models;

public record GraphRelationship(
    Guid Id,
    Guid FromMemorygramId,
    Guid ToMemorygramId,
    string RelationshipType,
    float Weight,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Properties = null,
    bool IsActive = true
);
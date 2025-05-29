using Mnemosyne.Core.Models;
using Shouldly;

namespace MemoryCore.Tests.UnitTests.Models;

public class GraphRelationshipTests
{
    [Fact]
    public void GraphRelationship_ShouldCreateWithAllProperties()
    {
        var id = Guid.NewGuid();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "RELATED_TO";
        var weight = 0.85f;
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var properties = "{\"category\": \"test\"}";
        var isActive = true;

        var relationship = new GraphRelationship(
            id,
            fromId,
            toId,
            relationshipType,
            weight,
            createdAt,
            updatedAt,
            properties,
            isActive
        );

        relationship.Id.ShouldBe(id);
        relationship.FromMemorygramId.ShouldBe(fromId);
        relationship.ToMemorygramId.ShouldBe(toId);
        relationship.RelationshipType.ShouldBe(relationshipType);
        relationship.Weight.ShouldBe(weight);
        relationship.CreatedAt.ShouldBe(createdAt);
        relationship.UpdatedAt.ShouldBe(updatedAt);
        relationship.Properties.ShouldBe(properties);
        relationship.IsActive.ShouldBe(isActive);
    }

    [Fact]
    public void GraphRelationship_ShouldCreateWithDefaultValues()
    {
        var id = Guid.NewGuid();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "SIMILAR_TO";
        var weight = 0.75f;
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow;

        var relationship = new GraphRelationship(
            id,
            fromId,
            toId,
            relationshipType,
            weight,
            createdAt,
            updatedAt
        );

        relationship.Id.ShouldBe(id);
        relationship.FromMemorygramId.ShouldBe(fromId);
        relationship.ToMemorygramId.ShouldBe(toId);
        relationship.RelationshipType.ShouldBe(relationshipType);
        relationship.Weight.ShouldBe(weight);
        relationship.CreatedAt.ShouldBe(createdAt);
        relationship.UpdatedAt.ShouldBe(updatedAt);
        relationship.Properties.ShouldBeNull();
        relationship.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void GraphRelationship_ShouldSupportRecordEquality()
    {
        var id = Guid.NewGuid();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "CONNECTS_TO";
        var weight = 0.9f;
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow;

        var relationship1 = new GraphRelationship(
            id,
            fromId,
            toId,
            relationshipType,
            weight,
            createdAt,
            updatedAt
        );

        var relationship2 = new GraphRelationship(
            id,
            fromId,
            toId,
            relationshipType,
            weight,
            createdAt,
            updatedAt
        );

        relationship1.ShouldBe(relationship2);
        relationship1.GetHashCode().ShouldBe(relationship2.GetHashCode());
    }

    [Fact]
    public void GraphRelationship_ShouldNotBeEqualWithDifferentValues()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "LINKS_TO";
        var weight = 0.8f;
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow;

        var relationship1 = new GraphRelationship(
            id1,
            fromId,
            toId,
            relationshipType,
            weight,
            createdAt,
            updatedAt
        );

        var relationship2 = new GraphRelationship(
            id2,
            fromId,
            toId,
            relationshipType,
            weight,
            createdAt,
            updatedAt
        );

        relationship1.ShouldNotBe(relationship2);
    }
}
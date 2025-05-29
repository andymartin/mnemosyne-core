using System;
using System.Text.Json;
using Mnemosyne.Core.Models;
using Shouldly;
using Xunit;

namespace MemoryCore.Tests.UnitTests.Models;

public class MemorygramTests
{
    [Fact]
    public void Memorygram_ShouldCreateWithSubtype()
    {
        // Arrange
        var id = Guid.NewGuid();
        var content = "Test content";
        var type = MemorygramType.Experience;
        var subtype = "personal_experience";
        var source = "Test";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow;

        // Act
        var memorygram = new Memorygram(
            Id: id,
            Content: content,
            Type: type,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: source,
            Timestamp: timestamp,
            CreatedAt: createdAt,
            UpdatedAt: updatedAt,
            Subtype: subtype
        );

        // Assert
        memorygram.Id.ShouldBe(id);
        memorygram.Content.ShouldBe(content);
        memorygram.Type.ShouldBe(type);
        memorygram.Subtype.ShouldBe(subtype);
        memorygram.Source.ShouldBe(source);
        memorygram.Timestamp.ShouldBe(timestamp);
        memorygram.CreatedAt.ShouldBe(createdAt);
        memorygram.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void Memorygram_ShouldCreateWithNullSubtype()
    {
        // Arrange
        var id = Guid.NewGuid();
        var content = "Test content";
        var type = MemorygramType.UserInput;

        // Act
        var memorygram = new Memorygram(
            Id: id,
            Content: content,
            Type: type,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: "Test",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        // Assert
        memorygram.Subtype.ShouldBeNull();
    }

    [Fact]
    public void Memorygram_ShouldNotHaveChatIdProperty()
    {
        // Arrange
        var memorygramType = typeof(Memorygram);

        // Act
        var chatIdProperty = memorygramType.GetProperty("ChatId");

        // Assert
        chatIdProperty.ShouldBeNull();
    }

    [Fact]
    public void Memorygram_ShouldHaveSubtypeProperty()
    {
        // Arrange
        var memorygramType = typeof(Memorygram);

        // Act
        var subtypeProperty = memorygramType.GetProperty("Subtype");

        // Assert
        subtypeProperty.ShouldNotBeNull();
        subtypeProperty.PropertyType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Memorygram_WithExperienceType_ShouldSerializeCorrectly()
    {
        // Arrange
        var memorygram = new Memorygram(
            Id: Guid.NewGuid(),
            Content: "Test experience",
            Type: MemorygramType.Experience,
            TopicalEmbedding: new float[] { 0.1f, 0.2f },
            ContentEmbedding: new float[] { 0.3f, 0.4f },
            ContextEmbedding: new float[] { 0.5f, 0.6f },
            MetadataEmbedding: new float[] { 0.7f, 0.8f },
            Source: "Test",
            Timestamp: 1234567890,
            CreatedAt: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Subtype: "learning_experience"
        );

        // Act
        var json = JsonSerializer.Serialize(memorygram);
        var deserialized = JsonSerializer.Deserialize<Memorygram>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Type.ShouldBe(MemorygramType.Experience);
        deserialized.Subtype.ShouldBe("learning_experience");
        deserialized.Content.ShouldBe("Test experience");
    }

    [Fact]
    public void Memorygram_WithReflectionType_ShouldSerializeCorrectly()
    {
        // Arrange
        var memorygram = new Memorygram(
            Id: Guid.NewGuid(),
            Content: "Test reflection",
            Type: MemorygramType.Reflection,
            TopicalEmbedding: new float[] { 0.1f, 0.2f },
            ContentEmbedding: new float[] { 0.3f, 0.4f },
            ContextEmbedding: new float[] { 0.5f, 0.6f },
            MetadataEmbedding: new float[] { 0.7f, 0.8f },
            Source: "Test",
            Timestamp: 1234567890,
            CreatedAt: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Subtype: "deep_reflection"
        );

        // Act
        var json = JsonSerializer.Serialize(memorygram);
        var deserialized = JsonSerializer.Deserialize<Memorygram>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Type.ShouldBe(MemorygramType.Reflection);
        deserialized.Subtype.ShouldBe("deep_reflection");
        deserialized.Content.ShouldBe("Test reflection");
    }

    [Theory]
    [InlineData(MemorygramType.Experience, "experience_subtype")]
    [InlineData(MemorygramType.Reflection, "reflection_subtype")]
    [InlineData(MemorygramType.UserInput, "user_subtype")]
    [InlineData(MemorygramType.AssistantResponse, "assistant_subtype")]
    public void Memorygram_WithDifferentTypesAndSubtypes_ShouldCreateCorrectly(MemorygramType type, string subtype)
    {
        // Act
        var memorygram = new Memorygram(
            Id: Guid.NewGuid(),
            Content: "Test content",
            Type: type,
            TopicalEmbedding: Array.Empty<float>(),
            ContentEmbedding: Array.Empty<float>(),
            ContextEmbedding: Array.Empty<float>(),
            MetadataEmbedding: Array.Empty<float>(),
            Source: "Test",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Subtype: subtype
        );

        // Assert
        memorygram.Type.ShouldBe(type);
        memorygram.Subtype.ShouldBe(subtype);
    }
}
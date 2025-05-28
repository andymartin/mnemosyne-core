using System;
using Shouldly;
using Mnemosyne.Core.Models;
using Xunit;

namespace Mnemosyne.Core.Tests.UnitTests.Models;

public class MemoryReformulationsTests
{
    [Fact]
    public void Indexer_ShouldReturnCorrectValue_WhenTypeIsValid()
    {
        // Arrange
        var reformulations = new MemoryReformulations
        {
            Topical = "Topical Test",
            Content = "Content Test",
            Context = "Context Test",
            Metadata = "Metadata Test"
        };

        // Act & Assert
        reformulations[MemoryReformulationType.Topical].ShouldBe("Topical Test");
        reformulations[MemoryReformulationType.Content].ShouldBe("Content Test");
        reformulations[MemoryReformulationType.Context].ShouldBe("Context Test");
        reformulations[MemoryReformulationType.Metadata].ShouldBe("Metadata Test");
    }

    [Fact]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTypeIsInvalid()
    {
        // Arrange
        var reformulations = new MemoryReformulations();
        var invalidType = (MemoryReformulationType)999; // An invalid enum value

        // Act
        Action act = () => _ = reformulations[invalidType];

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act)
            .Message.ShouldBe($"Not expected reformulation type value: {invalidType} (Parameter 'type')");
    }
}
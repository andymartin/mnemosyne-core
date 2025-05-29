using Mnemosyne.Core.Models;
using Shouldly;
using Xunit;

namespace MemoryCore.Tests.UnitTests.Models;

public class MemorygramTypeTests
{
    [Fact]
    public void MemorygramType_ShouldHaveExpectedValues()
    {
        // Arrange & Act
        var enumValues = Enum.GetValues<MemorygramType>();
        
        // Assert
        enumValues.ShouldContain(MemorygramType.Invalid);
        enumValues.ShouldContain(MemorygramType.UserInput);
        enumValues.ShouldContain(MemorygramType.AssistantResponse);
        enumValues.ShouldContain(MemorygramType.Experience);
        enumValues.ShouldContain(MemorygramType.Reflection);
        enumValues.Length.ShouldBe(5);
    }

    [Theory]
    [InlineData(MemorygramType.Invalid, "Invalid")]
    [InlineData(MemorygramType.UserInput, "UserInput")]
    [InlineData(MemorygramType.AssistantResponse, "AssistantResponse")]
    [InlineData(MemorygramType.Experience, "Experience")]
    [InlineData(MemorygramType.Reflection, "Reflection")]
    public void MemorygramType_ShouldHaveCorrectStringRepresentation(MemorygramType type, string expectedString)
    {
        // Act
        var stringValue = type.ToString();
        
        // Assert
        stringValue.ShouldBe(expectedString);
    }

    [Theory]
    [InlineData("Invalid", MemorygramType.Invalid)]
    [InlineData("UserInput", MemorygramType.UserInput)]
    [InlineData("AssistantResponse", MemorygramType.AssistantResponse)]
    [InlineData("Experience", MemorygramType.Experience)]
    [InlineData("Reflection", MemorygramType.Reflection)]
    public void MemorygramType_ShouldParseFromString(string stringValue, MemorygramType expectedType)
    {
        // Act
        var success = Enum.TryParse<MemorygramType>(stringValue, out var parsedType);
        
        // Assert
        success.ShouldBeTrue();
        parsedType.ShouldBe(expectedType);
    }

    [Fact]
    public void MemorygramType_Experience_ShouldHaveCorrectNumericValue()
    {
        // Act & Assert
        ((int)MemorygramType.Experience).ShouldBe(3);
    }

    [Fact]
    public void MemorygramType_Reflection_ShouldHaveCorrectNumericValue()
    {
        // Act & Assert
        ((int)MemorygramType.Reflection).ShouldBe(4);
    }
}
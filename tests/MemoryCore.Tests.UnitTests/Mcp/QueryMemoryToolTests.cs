using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Mcp;
using Mnemosyne.Core.Models;
using Moq;
using Shouldly;
using Xunit.Abstractions;

namespace MemoryCore.Tests.UnitTests.Mcp;

public class QueryMemoryToolTests
{
    private readonly Mock<IMemoryQueryService> _mockMemoryQueryService;
    private readonly Mock<ILogger<QueryMemoryTool>> _mockLogger;
    private readonly QueryMemoryTool _queryMemoryTool;
    private readonly ITestOutputHelper _output;

    public QueryMemoryToolTests(ITestOutputHelper output)
    {
        _output = output;
        _mockMemoryQueryService = new Mock<IMemoryQueryService>();
        _mockLogger = new Mock<ILogger<QueryMemoryTool>>();
        _queryMemoryTool = new QueryMemoryTool(_mockMemoryQueryService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task QueryMemoryAsync_WithValidInput_ReturnsSuccessResult()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", 5);
        var resultItems = new List<MemorygramResultItem>
        {
            new MemorygramResultItem(
                Guid.NewGuid(),
                "Test content",
                0.95f,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow)
        };
        var expectedResult = new MemoryQueryResult("success", resultItems, null);

        _mockMemoryQueryService
            .Setup(x => x.QueryAsync(input))
            .ReturnsAsync(Result.Ok(expectedResult));

        // Act
        _output.WriteLine("Executing QueryMemoryAsync with valid input");
        var result = await _queryMemoryTool.QueryMemoryAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("success");
        result.Results.ShouldNotBeNull();
        result.Results.Count.ShouldBe(1);
        result.Message.ShouldBeNull();

        _mockMemoryQueryService.Verify(x => x.QueryAsync(input), Times.Once);
    }

    [Fact]
    public async Task QueryMemoryAsync_WithNullInput_ReturnsErrorResult()
    {
        // Act
        _output.WriteLine("Executing QueryMemoryAsync with null input");
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = await _queryMemoryTool.QueryMemoryAsync(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("error");
        result.Results.ShouldBeNull();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("null");

        _mockMemoryQueryService.Verify(x => x.QueryAsync(It.IsAny<MemoryQueryInput>()), Times.Never);
    }

    [Fact]
    public async Task QueryMemoryAsync_WithEmptyQueryText_ReturnsErrorResult()
    {
        // Arrange
        var input = new MemoryQueryInput("", 5);

        // Act
        _output.WriteLine("Executing QueryMemoryAsync with empty query text");
        var result = await _queryMemoryTool.QueryMemoryAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("error");
        result.Results.ShouldBeNull();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("empty");

        _mockMemoryQueryService.Verify(x => x.QueryAsync(It.IsAny<MemoryQueryInput>()), Times.Never);
    }

    [Fact]
    public async Task QueryMemoryAsync_WhenServiceReturnsError_ReturnsErrorResult()
    {
        // Arrange
        var input = new MemoryQueryInput("test query", 5);
        var errorMessage = "Test error message";

        _mockMemoryQueryService
            .Setup(x => x.QueryAsync(input))
            .ReturnsAsync(Result.Fail(new Error(errorMessage)));

        // Act
        _output.WriteLine("Executing QueryMemoryAsync when service returns error");
        var result = await _queryMemoryTool.QueryMemoryAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("error");
        result.Results.ShouldBeNull();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain(errorMessage);

        _mockMemoryQueryService.Verify(x => x.QueryAsync(input), Times.Once);
    }
}
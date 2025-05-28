using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MemoryCore.Tests.UnitTests.Models.Pipelines;

public class MemoryRetrievalStageTests
{
    private readonly IMemoryQueryService _mockMemoryQueryService;
    private readonly ILogger<MemoryRetrievalStage> _mockLogger;
    private readonly MemoryRetrievalStage _stage;

    public MemoryRetrievalStageTests()
    {
        _mockMemoryQueryService = Substitute.For<IMemoryQueryService>();
        _mockLogger = Substitute.For<ILogger<MemoryRetrievalStage>>();
        _stage = new MemoryRetrievalStage(_mockMemoryQueryService, _mockLogger);
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Act
        var name = _stage.Name;

        // Assert
        name.ShouldBe("MemoryRetrievalStage");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulQuery_ShouldAddContextChunks()
    {
        // Arrange
        var userInput = "test query";
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Request = new PipelineExecutionRequest { UserInput = userInput }
        };

        var status = new PipelineExecutionStatus
        {
            RunId = state.RunId,
            PipelineId = state.PipelineId
        };

        var memorygrams = new List<MemorygramWithScore>
        {
            new(
                Id: Guid.NewGuid(),
                Content: "Test content 1",
                Type: MemorygramType.UserInput,
                TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                ContentEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                ContextEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                MetadataEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                Source: "Test source 1",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAt: DateTimeOffset.UtcNow,
                ChatId: null,
                PreviousMemorygramId: null,
                NextMemorygramId: null,
                Sequence: null,
                Score: 0.9f
            ),
            new(
                Id: Guid.NewGuid(),
                Content: "Test content 2",
                Type: MemorygramType.AssistantResponse,
                TopicalEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                ContextEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                MetadataEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                Source: "Test source 2",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-2),
                UpdatedAt: DateTimeOffset.UtcNow.AddHours(-1),
                ChatId: null,
                PreviousMemorygramId: null,
                NextMemorygramId: null,
                Sequence: null,
                Score: 0.8f
            )
        };

        _mockMemoryQueryService
            .QueryMemoryAsync(userInput, 5)
            .Returns(Result.Ok(memorygrams));

        // Act
        var result = await _stage.ExecuteAsync(state, status);

        // Assert
        result.ShouldBe(state);
        state.Context.Count.ShouldBe(2);

        var firstChunk = state.Context[0];
        firstChunk.Type.ShouldBe(ContextChunkType.Memory);
        firstChunk.Content.ShouldBe("Test content 1");
        firstChunk.RelevanceScore.ShouldBe(0.9f);
        firstChunk.Provenance.Source.ShouldBe("MemoryRetrievalStage");
        firstChunk.Provenance.OriginalId.ShouldBe(memorygrams[0].Id.ToString());
        firstChunk.Provenance.Timestamp.ShouldBe(memorygrams[0].UpdatedAt);
        firstChunk.Provenance.Metadata["MemorygramSource"].ShouldBe("Test source 1");
        firstChunk.Provenance.Metadata["MemorygramType"].ShouldBe("UserInput");

        var secondChunk = state.Context[1];
        secondChunk.Type.ShouldBe(ContextChunkType.Memory);
        secondChunk.Content.ShouldBe("Test content 2");
        secondChunk.RelevanceScore.ShouldBe(0.8f);
        secondChunk.Provenance.Source.ShouldBe("MemoryRetrievalStage");
        secondChunk.Provenance.OriginalId.ShouldBe(memorygrams[1].Id.ToString());
        secondChunk.Provenance.Timestamp.ShouldBe(memorygrams[1].UpdatedAt);
        secondChunk.Provenance.Metadata["MemorygramSource"].ShouldBe("Test source 2");
        secondChunk.Provenance.Metadata["MemorygramType"].ShouldBe("AssistantResponse");
    }

    [Fact]
    public async Task ExecuteAsync_WithMinimumSimilarityScore_ShouldFilterResults()
    {
        // Arrange
        var userInput = "test query";
        var minimumScore = 0.85f;
        var stageWithMinScore = new MemoryRetrievalStage(_mockMemoryQueryService, _mockLogger, minimumScore);
        
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Request = new PipelineExecutionRequest { UserInput = userInput }
        };

        var status = new PipelineExecutionStatus
        {
            RunId = state.RunId,
            PipelineId = state.PipelineId
        };

        var memorygrams = new List<MemorygramWithScore>
        {
            new(
                Id: Guid.NewGuid(),
                Content: "High score content",
                Type: MemorygramType.UserInput,
                TopicalEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                ContentEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                ContextEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                MetadataEmbedding: new float[] { 0.1f, 0.2f, 0.3f },
                Source: "Test source 1",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                ChatId: null,
                PreviousMemorygramId: null,
                NextMemorygramId: null,
                Sequence: null,
                Score: 0.9f
            ),
            new(
                Id: Guid.NewGuid(),
                Content: "Low score content",
                Type: MemorygramType.AssistantResponse,
                TopicalEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                ContentEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                ContextEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                MetadataEmbedding: new float[] { 0.4f, 0.5f, 0.6f },
                Source: "Test source 2",
                Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow,
                ChatId: null,
                PreviousMemorygramId: null,
                NextMemorygramId: null,
                Sequence: null,
                Score: 0.7f
            )
        };

        _mockMemoryQueryService
            .QueryMemoryAsync(userInput, 5)
            .Returns(Result.Ok(memorygrams));

        // Act
        var result = await stageWithMinScore.ExecuteAsync(state, status);

        // Assert
        result.ShouldBe(state);
        state.Context.Count.ShouldBe(1);
        state.Context[0].Content.ShouldBe("High score content");
        state.Context[0].RelevanceScore.ShouldBe(0.9f);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoResults_ShouldSucceedWithEmptyContext()
    {
        // Arrange
        var userInput = "test query";
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Request = new PipelineExecutionRequest { UserInput = userInput }
        };

        var status = new PipelineExecutionStatus
        {
            RunId = state.RunId,
            PipelineId = state.PipelineId
        };

        _mockMemoryQueryService
            .QueryMemoryAsync(userInput, 5)
            .Returns(Result.Ok(new List<MemorygramWithScore>()));

        // Act
        var result = await _stage.ExecuteAsync(state, status);

        // Assert
        result.ShouldBe(state);
        state.Context.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithQueryFailure_ShouldReturnStateUnchanged()
    {
        // Arrange
        var userInput = "test query";
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Request = new PipelineExecutionRequest { UserInput = userInput }
        };

        var status = new PipelineExecutionStatus
        {
            RunId = state.RunId,
            PipelineId = state.PipelineId
        };

        var error = "Query failed";
        _mockMemoryQueryService
            .QueryMemoryAsync(userInput, 5)
            .Returns(Result.Fail(error));

        // Act
        var result = await _stage.ExecuteAsync(state, status);

        // Assert
        result.ShouldBe(state);
        state.Context.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldReturnStateUnchanged()
    {
        // Arrange
        var userInput = "test query";
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Request = new PipelineExecutionRequest { UserInput = userInput }
        };

        var status = new PipelineExecutionStatus
        {
            RunId = state.RunId,
            PipelineId = state.PipelineId
        };

        _mockMemoryQueryService
            .QueryMemoryAsync(userInput, 5)
            .Returns<Result<List<MemorygramWithScore>>>(x => throw new InvalidOperationException("Test exception"));

        // Act
        var result = await _stage.ExecuteAsync(state, status);

        // Assert
        result.ShouldBe(state);
        state.Context.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOrEmptyUserInput_ShouldReturnStateUnchanged()
    {
        // Arrange
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Request = new PipelineExecutionRequest { UserInput = "" }
        };

        var status = new PipelineExecutionStatus
        {
            RunId = state.RunId,
            PipelineId = state.PipelineId
        };

        // Act
        var result = await _stage.ExecuteAsync(state, status);

        // Assert
        result.ShouldBe(state);
        state.Context.Count.ShouldBe(0);
        
        // Verify that QueryMemoryAsync was not called
        await _mockMemoryQueryService.DidNotReceive().QueryMemoryAsync(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallQueryMemoryWithCorrectParameters()
    {
        // Arrange
        var userInput = "test query";
        var state = new PipelineExecutionState
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Request = new PipelineExecutionRequest { UserInput = userInput }
        };

        var status = new PipelineExecutionStatus
        {
            RunId = state.RunId,
            PipelineId = state.PipelineId
        };

        _mockMemoryQueryService
            .QueryMemoryAsync(userInput, 5)
            .Returns(Result.Ok(new List<MemorygramWithScore>()));

        // Act
        await _stage.ExecuteAsync(state, status);

        // Assert
        await _mockMemoryQueryService.Received(1).QueryMemoryAsync(userInput, 5);
    }
}
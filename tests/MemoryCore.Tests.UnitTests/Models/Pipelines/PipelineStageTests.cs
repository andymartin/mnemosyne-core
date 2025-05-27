using Mnemosyne.Core.Models.Pipelines;
using Shouldly;

namespace MemoryCore.Tests.UnitTests.Models.Pipelines;

public class PipelineStageTests
{
    private class TestPipelineStage : PipelineStage
    {
        public bool InternalExecuteCalled { get; private set; }
        public PipelineExecutionState? ReceivedState { get; private set; }

        protected override Task<PipelineExecutionState> ExecuteInternalAsync(PipelineExecutionState state)
        {
            InternalExecuteCalled = true;
            ReceivedState = state;

            // Add test information to context
            state.Context.Add(new ContextChunk
            {
                Type = ContextChunkType.UserInput,
                Provenance = "TestPipelineStage",
                Content = "Test component executed"
            });

            return Task.FromResult(state);
        }
    }

    [Fact]
    public void PipelineComponent_Constructor_SetsNameCorrectly()
    {
        // Arrange & Act
        var component = new TestPipelineStage();

        // Assert
        component.Name.ShouldBe(nameof(TestPipelineStage));
    }

    [Fact]
    public async Task ExecuteAsync_SetsCurrentStageNameAndStartTime_AndCallsExecuteInternal()
    {
        // Arrange
        var component = new TestPipelineStage();
        var request = new PipelineExecutionRequest
        {
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>(),
            ResponseChannelMetadata = new Dictionary<string, object>()
        };
        var state = new PipelineExecutionState { Request = request };
        var status = new PipelineExecutionStatus
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Status = PipelineStatus.Pending,
            OverallStartTime = DateTime.UtcNow
        };
        var startTimeBeforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await component.ExecuteAsync(state, status);

        // Assert
        status.CurrentStageName.ShouldBe(component.Name);
        status.CurrentStageStartTime.ShouldNotBeNull();
        status.CurrentStageStartTime.Value.ShouldBe(startTimeBeforeExecution.DateTime, TimeSpan.FromSeconds(1));
        component.InternalExecuteCalled.ShouldBeTrue();
        component.ReceivedState.ShouldNotBeNull();
        component.ReceivedState.ShouldBeSameAs(state);
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(state);
        result.Context.ShouldNotBeEmpty();
        result.Context.Last().Type.ShouldBe(ContextChunkType.UserInput);
        result.Context.Last().Provenance.ShouldBe("TestPipelineStage");
        result.Context.Last().Content.ShouldBe("Test component executed");
    }

    [Fact]
    public void NullPipelineStage_Constructor_SetsNameCorrectly()
    {
        // Arrange & Act
        var nullStage = new NullPipelineStage();

        // Assert
        nullStage.Name.ShouldBe(nameof(NullPipelineStage));
    }

    [Fact]
    public async Task NullPipelineStage_ExecuteInternalAsync_UpdatesStateAfterDelay()
    {
        // Arrange
        var nullStage = new NullPipelineStage();
        var request = new PipelineExecutionRequest
        {
            UserInput = "test input",
            SessionMetadata = new Dictionary<string, object>(),
            ResponseChannelMetadata = new Dictionary<string, object>()
        };
        var state = new PipelineExecutionState { Request = request };
        var status = new PipelineExecutionStatus
        {
            RunId = Guid.NewGuid(),
            PipelineId = Guid.NewGuid(),
            Status = PipelineStatus.Processing,
            OverallStartTime = DateTime.UtcNow,
            CurrentStageName = "PreviousStage",
            CurrentStageStartTime = DateTime.UtcNow.AddMinutes(-1)
        };

        // Act
        var result = await nullStage.ExecuteAsync(state, status);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(state);
        result.Context.ShouldNotBeEmpty();
        result.Context.Last().Type.ShouldBe(ContextChunkType.Simulation);
        result.Context.Last().Provenance.ShouldBe("NullPipelineStage");
        result.Context.Last().Content.ShouldBe("Simulated stage completed");
        status.CurrentStageName.ShouldBe(nameof(NullPipelineStage));
    }
}

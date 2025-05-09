using Mnemosyne.Core.Models.Pipelines;
using Shouldly;

namespace Mnemosyne.Core.Tests.Models.Pipelines;

public class PipelineComponentTests
{
    private class TestPipelineComponent : PipelineComponent
    {
        public bool InternalExecuteCalled { get; private set; }
        public PipelineExecutionState? ReceivedState { get; private set; }

        protected override Task<PipelineExecutionResult> ExecuteInternalAsync(PipelineExecutionState state)
        {
            InternalExecuteCalled = true;
            ReceivedState = state;
            return Task.FromResult(new PipelineExecutionResult
            {
                ResponseMessage = "Test component executed",
                UpdatedMetadata = new Dictionary<string, object>()
            });
        }
    }

    [Fact]
    public void PipelineComponent_Constructor_SetsNameCorrectly()
    {
        // Arrange & Act
        var component = new TestPipelineComponent();

        // Assert
        component.Name.ShouldBe(nameof(TestPipelineComponent));
    }

    [Fact]
    public async Task ExecuteAsync_SetsCurrentStageNameAndStartTime_AndCallsExecuteInternal()
    {
        // Arrange
        var component = new TestPipelineComponent();
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
        result.ResponseMessage.ShouldBe("Test component executed");
        (result.UpdatedMetadata as Dictionary<string, object>).ShouldNotBeNull();
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
    public async Task NullPipelineStage_ExecuteInternalAsync_ReturnsExpectedResultAfterDelay()
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
        result.ResponseMessage.ShouldBe("Simulated stage completed");
        (result.UpdatedMetadata as Dictionary<string, object>).ShouldNotBeNull();
        (result.UpdatedMetadata as Dictionary<string, object>).ShouldBeEmpty();
        status.CurrentStageName.ShouldBe(nameof(NullPipelineStage));
    }
}

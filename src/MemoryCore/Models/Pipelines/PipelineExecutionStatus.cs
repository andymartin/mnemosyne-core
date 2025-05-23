namespace Mnemosyne.Core.Models.Pipelines;

/// <summary>
/// Represents the current status and metadata of a cognitive pipeline execution run.
/// </summary>
public class PipelineExecutionStatus
{
    /// <summary>
    /// Gets or sets the unique identifier for this specific execution run.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the pipeline definition being executed.
    /// </summary>
    public Guid PipelineId { get; set; }

    /// <summary>
    /// Gets or sets the current overall status of the pipeline execution.
    /// </summary>
    public PipelineStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the name of the current stage being processed. Null if not in a specific stage.
    /// </summary>
    public string? CurrentStageName { get; set; }

    /// <summary>
    /// Gets or sets the start time of the current stage. Null if not in a specific stage.
    /// Uses DateTime; consider DateTimeOffset for timezone precision if needed across systems.
    /// </summary>
    public DateTimeOffset? CurrentStageStartTime { get; set; }

    /// <summary>
    /// Gets or sets the overall start time of the pipeline execution.
    /// Uses DateTime; consider DateTimeOffset for timezone precision if needed across systems.
    /// </summary>
    public DateTimeOffset OverallStartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the pipeline execution. Null if not yet completed or failed.
    /// Uses DateTime; consider DateTimeOffset for timezone precision if needed across systems.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the message or result information for the pipeline execution.
    /// Contains success message if completed successfully or error information if failed.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets the history of each stage's execution within the pipeline.
    /// </summary>
    public List<PipelineStageHistory> StageHistory { get; } = new List<PipelineStageHistory>();

    /// <summary>
    /// Adds a new entry to the stage history.
    /// </summary>
    /// <param name="stageName">The name of the stage.</param>
    /// <param name="result">The result of the stage execution.</param>
    /// <param name="message">An optional message describing the stage's outcome.</param>
    public void AddStageHistory(string stageName, StageResult result, string? message = null)
    {
        StageHistory.Add(new PipelineStageHistory
        {
            StageName = stageName,
            Result = result,
            Timestamp = DateTimeOffset.UtcNow,
            Message = message
        });
    }
}

/// <summary>
/// Represents the outcome of a single pipeline stage execution.
/// </summary>
public enum StageResult
{
    /// <summary>
    /// The stage completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The stage encountered an error.
    /// </summary>
    Error,

    /// <summary>
    /// The stage was skipped based on its internal logic.
    /// </summary>
    Skipped
}

namespace Mnemosyne.Core.Models.Pipelines;

/// <summary>
/// Defines the possible statuses of a cognitive pipeline execution.
/// </summary>
public enum PipelineStatus
{
    /// <summary>
    /// The pipeline execution request has been received and is awaiting processing.
    /// </summary>
    Pending,
    /// <summary>
    /// The pipeline execution is actively running.
    /// </summary>
    Running,
    /// <summary>
    /// The pipeline is currently processing a specific stage.
    /// </summary>
    Processing,
    /// <summary>
    /// The pipeline execution has completed successfully.
    /// </summary>
    Completed,
    /// <summary>
    /// The pipeline execution has failed.
    /// </summary>
    Failed
}

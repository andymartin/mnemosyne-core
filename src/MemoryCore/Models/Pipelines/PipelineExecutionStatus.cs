namespace Mnemosyne.Core.Models.Pipelines
{
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
        /// Gets or sets the result of the pipeline execution, if completed successfully.
        /// </summary>
        public PipelineExecutionResult? Result { get; set; }

        /// <summary>
        /// Gets or sets error information if the pipeline execution failed.
        /// This could be a simple string, an exception object, or a custom error DTO.
        /// </summary>
        public string? Message { get; set; }
    }
}

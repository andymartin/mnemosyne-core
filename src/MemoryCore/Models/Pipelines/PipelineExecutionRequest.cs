

// Required for Dictionary

namespace Mnemosyne.Core.Models.Pipelines;

/// <summary>
/// Represents the data required to initiate the execution of a cognitive pipeline.
/// </summary>
public class PipelineExecutionRequest
{
    /// <summary>
    /// Gets or sets the ID of the pipeline to be executed.
    /// </summary>
    public Guid PipelineId { get; set; }

    /// <summary>
    /// Gets or sets the primary user input for the pipeline.
    /// </summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets metadata related to the current user session.
    /// </summary>
    public Dictionary<string, object> SessionMetadata { get; set; } = new();

    /// <summary>
    /// Gets or sets metadata related to the channel through which the response will be sent.
    /// </summary>
    public Dictionary<string, object> ResponseChannelMetadata { get; set; } = new();
}
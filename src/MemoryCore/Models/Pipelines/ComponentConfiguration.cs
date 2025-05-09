using Mnemosyne.Core.Interfaces;

namespace Mnemosyne.Core.Models.Pipelines;

/// <summary>
/// Represents the configuration for a single component within a cognitive pipeline.
/// </summary>
public class ComponentConfiguration
{
    /// <summary>
    /// Gets or sets the unique name or identifier for this component instance within the pipeline.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type or key that identifies the specific <see cref="IPipelineStage"/> implementation to be used.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a dictionary of specific configuration parameters for this component instance.
    /// </summary>
    public Dictionary<string, object> Config { get; set; } = new();
}

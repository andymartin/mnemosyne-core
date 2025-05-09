namespace Mnemosyne.Core.Models.Pipelines
{
    /// <summary>
    /// Represents the definition of a cognitive pipeline.
    /// </summary>
    public class PipelineManifest
    {
        /// <summary>
        /// Gets or sets the unique identifier for the pipeline.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the human-readable name of the pipeline.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a description of the pipeline's purpose and functionality.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of component configurations that make up this pipeline.
        /// </summary>
        public List<ComponentConfiguration> Components { get; set; } = new();
    }
}
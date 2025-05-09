using Microsoft.AspNetCore.Mvc;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;

namespace Mnemosyne.Core.Controllers
{
    [ApiController]
    [Route("api/pipelines")]
    public class PipelinesController : ControllerBase
    {
        private readonly IPipelinesService _pipelineService;
        private readonly ILogger<PipelinesController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelinesController"/> class.
        /// </summary>
        /// <param name="pipelineService">The pipeline service.</param>
        /// <param name="logger">The logger.</param>
        public PipelinesController(IPipelinesService pipelineService, ILogger<PipelinesController> logger)
        {
            _pipelineService = pipelineService ?? throw new ArgumentNullException(nameof(pipelineService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves all available cognitive pipeline definitions.
        /// </summary>
        /// <returns>A list of pipeline manifests.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PipelineManifest>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<PipelineManifest>>> GetAllPipelines()
        {
            _logger.LogInformation("Attempting to get all pipelines.");
            var result = await _pipelineService.GetAllPipelinesAsync();
            if (result.IsFailed)
            {
                _logger.LogWarning("Failed to get all pipelines: {Errors}", string.Join(", ", result.Errors.Select(e => e.Message)));
                return Problem(
                    title: "Failed to retrieve pipelines",
                    detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            _logger.LogInformation("Successfully retrieved {Count} pipelines.", result.Value.Count());
            return Ok(result.Value);
        }

        /// <summary>
        /// Retrieves the definition of a specific cognitive pipeline.
        /// </summary>
        /// <param name="pipelineId">The ID of the pipeline to retrieve.</param>
        /// <returns>The pipeline manifest.</returns>
        [HttpGet("{pipelineId:guid}")]
        [ProducesResponseType(typeof(PipelineManifest), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PipelineManifest>> GetPipeline(Guid pipelineId)
        {
            _logger.LogInformation("Attempting to get pipeline with ID: {PipelineId}", pipelineId);
            var result = await _pipelineService.GetPipelineAsync(pipelineId);
            if (result.IsFailed)
            {
                if (result.Errors.Any(e => e.Message.ToLower().Contains("not found")))
                {
                    _logger.LogWarning("Pipeline with ID {PipelineId} not found.", pipelineId);
                    return NotFound(new ProblemDetails { Title = "Pipeline not found", Detail = $"Pipeline with ID {pipelineId} was not found."});
                }
                _logger.LogWarning("Failed to get pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return Problem(
                    title: "Failed to retrieve pipeline",
                    detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            _logger.LogInformation("Successfully retrieved pipeline with ID: {PipelineId}", pipelineId);
            return Ok(result.Value);
        }

        /// <summary>
        /// Creates a new cognitive pipeline definition.
        /// </summary>
        /// <param name="manifest">The pipeline manifest to create.</param>
        /// <returns>The created pipeline manifest.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(PipelineManifest), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PipelineManifest>> CreatePipeline([FromBody] PipelineManifest manifest)
        {
            if (manifest == null)
            {
                _logger.LogWarning("CreatePipeline request received with null manifest.");
                return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Pipeline manifest cannot be null." });
            }
            _logger.LogInformation("Attempting to create pipeline: {PipelineName}", manifest.Name);

            var result = await _pipelineService.CreatePipelineAsync(manifest);
            if (result.IsFailed)
            {
                if (result.Errors.Any(e => e.Message.ToLower().Contains("already exists"))) {
                    _logger.LogWarning("Failed to create pipeline {PipelineName} - already exists: {Errors}", manifest.Name, string.Join(", ", result.Errors.Select(e => e.Message)));
                    return Conflict(new ProblemDetails { Title = "Pipeline already exists", Detail = string.Join(", ", result.Errors.Select(e => e.Message))});
                }
                _logger.LogWarning("Failed to create pipeline {PipelineName}: {Errors}", manifest.Name, string.Join(", ", result.Errors.Select(e => e.Message)));
                return Problem(
                    title: "Failed to create pipeline",
                    detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            _logger.LogInformation("Successfully created pipeline {PipelineName} with ID {PipelineId}", result.Value.Name, result.Value.Id);
            return Created($"/api/pipelines/{result.Value.Id}", result.Value);
        }

        /// <summary>
        /// Updates an existing cognitive pipeline definition.
        /// </summary>
        /// <param name="pipelineId">The ID of the pipeline to update.</param>
        /// <param name="manifest">The updated pipeline manifest.</param>
        /// <returns>An IActionResult indicating the result of the operation.</returns>
        [HttpPut("{pipelineId:guid}")]
        [ProducesResponseType(typeof(PipelineManifest), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdatePipeline(Guid pipelineId, [FromBody] PipelineManifest manifest)
        {
            if (manifest == null)
            {
                _logger.LogWarning("UpdatePipeline request received with null manifest for ID: {PipelineId}.", pipelineId);
                return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Pipeline manifest cannot be null." });
            }
            if (pipelineId == Guid.Empty) {
                 _logger.LogWarning("UpdatePipeline request received with empty pipelineId.");
                return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Pipeline ID cannot be empty." });
            }
            manifest.Id = pipelineId;

            _logger.LogInformation("Attempting to update pipeline with ID: {PipelineId} using manifest named: {ManifestName}", pipelineId, manifest.Name);
            var result = await _pipelineService.UpdatePipelineAsync(pipelineId, manifest);
            if (result.IsFailed)
            {
                if (result.Errors.Any(e => e.Message.ToLower().Contains("not found")))
                {
                     _logger.LogWarning("Pipeline with ID {PipelineId} not found for update.", pipelineId);
                    return NotFound(new ProblemDetails { Title = "Pipeline not found", Detail = $"Pipeline with ID {pipelineId} was not found for update."});
                }
                _logger.LogWarning("Failed to update pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return Problem(
                    title: "Failed to update pipeline",
                    detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            _logger.LogInformation("Successfully updated pipeline with ID: {PipelineId}", pipelineId);
            return Ok(result.Value);
        }

        /// <summary>
        /// Deletes a cognitive pipeline definition.
        /// </summary>
        /// <param name="pipelineId">The ID of the pipeline to delete.</param>
        /// <returns>An IActionResult indicating the result of the operation.</returns>
        [HttpDelete("{pipelineId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeletePipeline(Guid pipelineId)
        {
            if (pipelineId == Guid.Empty) {
                 _logger.LogWarning("DeletePipeline request received with empty pipelineId.");
                return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Pipeline ID cannot be empty." });
            }
            _logger.LogInformation("Attempting to delete pipeline with ID: {PipelineId}", pipelineId);

            var result = await _pipelineService.DeletePipelineAsync(pipelineId);
            if (result.IsFailed)
            {
                 if (result.Errors.Any(e => e.Message.ToLower().Contains("not found")))
                {
                    _logger.LogWarning("Pipeline with ID {PipelineId} not found for deletion.", pipelineId);
                    return NotFound(new ProblemDetails { Title = "Pipeline not found", Detail = $"Pipeline with ID {pipelineId} was not found for deletion."});
                }
                _logger.LogWarning("Failed to delete pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return Problem(
                    title: "Failed to delete pipeline",
                    detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            _logger.LogInformation("Successfully deleted pipeline with ID: {PipelineId}", pipelineId);
            return NoContent();
        }

        /// <summary>
        /// Asynchronously initiates the execution of the specified cognitive pipeline.
        /// </summary>
        /// <param name="pipelineId">The ID of the pipeline to execute.</param>
        /// <param name="request">The pipeline execution request data.</param>
        /// <returns>The initial status of the pipeline execution.</returns>
        [HttpPost("{pipelineId:guid}/execute")]
        [ProducesResponseType(typeof(PipelineExecutionStatus), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PipelineExecutionStatus>> ExecutePipeline(
            Guid pipelineId,
            [FromBody] PipelineExecutionRequest request)
        {
            if (request == null)
            {
                _logger.LogWarning("ExecutePipeline request received with null body for Pipeline ID: {PipelineId}.", pipelineId);
                return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Execution request body cannot be null." });
            }
             if (pipelineId == Guid.Empty) {
                 _logger.LogWarning("ExecutePipeline request received with empty pipelineId.");
                return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Pipeline ID cannot be empty." });
            }
            _logger.LogInformation("Attempting to execute pipeline ID: {PipelineId}", pipelineId);

            var result = await _pipelineService.ExecutePipelineAsync(pipelineId, request);
            if (result.IsFailed)
            {
                if (result.Errors.Any(e => e.Message.ToLower().Contains("not found")))
                {
                     _logger.LogWarning("Cannot execute pipeline ID {PipelineId} - not found.", pipelineId);
                    return NotFound(new ProblemDetails { Title = "Pipeline not found", Detail = $"Pipeline with ID {pipelineId} was not found for execution."});
                }
                _logger.LogWarning("Failed to initiate execution for pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return Problem(
                    title: "Failed to initiate pipeline execution",
                    detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            
            _logger.LogInformation("Pipeline ID: {PipelineId} execution accepted with RunId: {RunId}", pipelineId, result.Value.RunId);
            var locationUrl = $"/api/pipelines/{pipelineId}/status/{result.Value.RunId}";
            return Accepted(locationUrl, result.Value);
        }

        /// <summary>
        /// Retrieves the current status and result (if available) of a specific pipeline execution run.
        /// </summary>
        /// <param name="pipelineId">The ID of the pipeline.</param>
        /// <param name="runId">The ID of the execution run.</param>
        /// <returns>The current status of the pipeline execution.</returns>
        [HttpGet("{pipelineId:guid}/status/{runId:guid}")]
        [ProducesResponseType(typeof(PipelineExecutionStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<PipelineExecutionStatus> GetExecutionStatus(Guid pipelineId, Guid runId)
        {
            if (runId == Guid.Empty) {
                 _logger.LogWarning("GetExecutionStatus request received with empty runId for Pipeline ID: {PipelineId}.", pipelineId);
                return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Run ID cannot be empty." });
            }
            _logger.LogInformation("Attempting to get execution status for Pipeline ID: {PipelineId}, RunId: {RunId}", pipelineId, runId);

            var result = _pipelineService.GetExecutionStatus(runId);
            if (result.IsFailed)
            {
                _logger.LogWarning("Execution status not found for Pipeline ID: {PipelineId}, RunId: {RunId}", pipelineId, runId);
                return NotFound(new ProblemDetails { Title = "Execution status not found", Detail = $"Execution status for Run ID {runId} (Pipeline ID {pipelineId}) was not found."});
            }
            _logger.LogInformation("Successfully retrieved execution status for Pipeline ID: {PipelineId}, RunId: {RunId}", pipelineId, runId);
            return Ok(result.Value);
        }
    }
}

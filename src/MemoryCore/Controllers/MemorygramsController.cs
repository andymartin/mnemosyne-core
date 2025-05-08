using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MemoryCore.Models;
using MemoryCore.Services;
using FluentResults;
using System.ComponentModel.DataAnnotations;

namespace MemoryCore.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MemorygramsController : ControllerBase
    {
        private readonly IMemorygramService _memorygramService;
        private readonly ILogger<MemorygramsController> _logger;

        public MemorygramsController(IMemorygramService memorygramService, ILogger<MemorygramsController> logger)
        {
            _memorygramService = memorygramService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new Memorygram
        /// </summary>
        /// <param name="request">The Memorygram creation request</param>
        /// <returns>The created Memorygram</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateMemorygram([FromBody] CreateMemorygramRequest request)
        {
            if (string.IsNullOrEmpty(request.Content))
            {
                return BadRequest("Content is required");
            }

            try
            {
                var memorygram = new Memorygram(
                    Guid.NewGuid(),
                    request.Content,
                    request.VectorEmbedding ?? Array.Empty<float>(),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow
                );

                var result = await _memorygramService.CreateOrUpdateMemorygramAsync(memorygram);

                if (result.IsSuccess)
                {
                    return CreatedAtAction(nameof(GetMemorygram), new { id = result.Value.Id }, result.Value);
                }
                else
                {
                    _logger.LogError("Failed to create memorygram: {Errors}", string.Join(", ", result.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating memorygram");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the memorygram");
            }
        }

        /// <summary>
        /// Retrieves a Memorygram by ID
        /// </summary>
        /// <param name="id">The ID of the Memorygram to retrieve</param>
        /// <returns>The requested Memorygram</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMemorygram(Guid id)
        {
            try
            {
                var result = await _memorygramService.GetMemorygramByIdAsync(id);

                if (result.IsSuccess)
                {
                    return Ok(result.Value);
                }
                else
                {
                    if (result.Errors.Any(e => e.Message.Contains("not found")))
                    {
                        return NotFound($"Memorygram with ID {id} not found");
                    }
                    else
                    {
                        _logger.LogError("Failed to retrieve memorygram: {Errors}", string.Join(", ", result.Errors));
                        return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving memorygram with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the memorygram");
            }
        }

        /// <summary>
        /// Updates an existing Memorygram
        /// </summary>
        /// <param name="id">The ID of the Memorygram to update</param>
        /// <param name="request">The Memorygram update request</param>
        /// <returns>The updated Memorygram</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateMemorygram(Guid id, [FromBody] UpdateMemorygramRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if ID is empty
            if (id == Guid.Empty)
            {
                return BadRequest("Memorygram ID is required in the route.");
            }
            
            // Retrieve the existing memorygram to apply partial updates if needed
            var existingMemorygramResult = await _memorygramService.GetMemorygramByIdAsync(id);

            if (existingMemorygramResult.IsFailed)
            {
                if (existingMemorygramResult.Errors.Any(e => e.Message.Contains("not found")))
                {
                    return NotFound($"Memorygram with ID {id} not found");
                }
                else
                {
                    _logger.LogError("Failed to retrieve existing memorygram for update: {Errors}", string.Join(", ", existingMemorygramResult.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, existingMemorygramResult.Errors);
                }
            }

            var existingMemorygram = existingMemorygramResult.Value;

            // Validate at least one non-empty update parameter
            if ((string.IsNullOrWhiteSpace(request.Content) || request.Content == string.Empty)
                && request.VectorEmbedding == null)
            {
                return BadRequest(new {
                    Error = "InvalidRequest",
                    Message = "At least one valid update parameter must be provided (non-empty Content or VectorEmbedding)"
                });
            }

            // Create a new Memorygram object with updated values
            var updatedMemorygram = new Memorygram(
                existingMemorygram.Id,
                request.Content ?? existingMemorygram.Content, // Use new content if provided, otherwise keep existing
                request.VectorEmbedding ?? existingMemorygram.VectorEmbedding, // Use new embedding if provided, otherwise keep existing
                existingMemorygram.CreatedAt,
                DateTimeOffset.UtcNow
            );

            try
            {
                var result = await _memorygramService.CreateOrUpdateMemorygramAsync(updatedMemorygram);

                if (result.IsSuccess)
                {
                    return Ok(result.Value);
                }
                else
                {
                    _logger.LogError("Failed to update memorygram: {Errors}", string.Join(", ", result.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating memorygram with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the memorygram");
            }
        }

        /// <summary>
        /// Updates an existing Memorygram with partial updates (PATCH)
        /// </summary>
        /// <param name="id">The ID of the Memorygram to update</param>
        /// <param name="request">The Memorygram update request</param>
        /// <returns>The updated Memorygram</returns>
        [HttpPatch("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PatchMemorygram(Guid id, [FromBody] UpdateMemorygramRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if ID is empty
            if (id == Guid.Empty)
            {
                return BadRequest("Memorygram ID is required in the route.");
            }
            
            // Retrieve the existing memorygram to apply partial updates
            var existingMemorygramResult = await _memorygramService.GetMemorygramByIdAsync(id);

            if (existingMemorygramResult.IsFailed)
            {
                if (existingMemorygramResult.Errors.Any(e => e.Message.Contains("not found")))
                {
                    return NotFound($"Memorygram with ID {id} not found");
                }
                else
                {
                    _logger.LogError("Failed to retrieve existing memorygram for update: {Errors}", string.Join(", ", existingMemorygramResult.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, existingMemorygramResult.Errors);
                }
            }

            var existingMemorygram = existingMemorygramResult.Value;

            // Validate at least one non-empty update parameter
            if ((string.IsNullOrWhiteSpace(request.Content) || request.Content == string.Empty)
                && request.VectorEmbedding == null)
            {
                return BadRequest(new {
                    Error = "InvalidRequest",
                    Message = "At least one valid update parameter must be provided (non-empty Content or VectorEmbedding)"
                });
            }

            // Create a new Memorygram object with updated values
            var updatedMemorygram = new Memorygram(
                existingMemorygram.Id,
                request.Content ?? existingMemorygram.Content, // Use new content if provided, otherwise keep existing
                request.VectorEmbedding ?? existingMemorygram.VectorEmbedding, // Use new embedding if provided, otherwise keep existing
                existingMemorygram.CreatedAt,
                DateTimeOffset.UtcNow
            );

            try
            {
                var result = await _memorygramService.CreateOrUpdateMemorygramAsync(updatedMemorygram);

                if (result.IsSuccess)
                {
                    return Ok(result.Value);
                }
                else
                {
                    _logger.LogError("Failed to update memorygram: {Errors}", string.Join(", ", result.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating memorygram with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the memorygram");
            }
        }
        /// <summary>
        /// Creates an association between two Memorygrams
        /// </summary>
        /// <param name="id">The ID of the source Memorygram</param>
        /// <param name="request">The association request</param>
        /// <returns>The source Memorygram with the created association</returns>
        [HttpPost("{id}/associate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateAssociation(Guid id, [FromBody] CreateAssociationRequest request)
        {
            // Check if source ID is empty
            if (id == Guid.Empty)
            {
                return BadRequest("Source Memorygram ID is required in the route.");
            }

            // Check if target ID is empty
            if (request.TargetId == Guid.Empty)
            {
                return BadRequest("Target Memorygram ID is required in the request body.");
            }

            // Verify the source Memorygram exists
            var sourceMemorygramResult = await _memorygramService.GetMemorygramByIdAsync(id);
            if (sourceMemorygramResult.IsFailed)
            {
                if (sourceMemorygramResult.Errors.Any(e => e.Message.Contains("not found")))
                {
                    return NotFound($"Source Memorygram with ID {id} not found");
                }
                else
                {
                    _logger.LogError("Failed to retrieve source memorygram: {Errors}", string.Join(", ", sourceMemorygramResult.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, sourceMemorygramResult.Errors);
                }
            }

            // Verify the target Memorygram exists
            var targetMemorygramResult = await _memorygramService.GetMemorygramByIdAsync(request.TargetId);
            if (targetMemorygramResult.IsFailed)
            {
                if (targetMemorygramResult.Errors.Any(e => e.Message.Contains("not found")))
                {
                    return NotFound($"Target Memorygram with ID {request.TargetId} not found");
                }
                else
                {
                    _logger.LogError("Failed to retrieve target memorygram: {Errors}", string.Join(", ", targetMemorygramResult.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, targetMemorygramResult.Errors);
                }
            }

            // Create the association
            try
            {
                var result = await _memorygramService.CreateAssociationAsync(id, request.TargetId, request.Weight);

                if (result.IsSuccess)
                {
                    return Ok(result.Value);
                }
                else
                {
                    _logger.LogError("Failed to create association: {Errors}", string.Join(", ", result.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating association between {SourceId} and {TargetId}", id, request.TargetId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the association");
            }
        }
    }

    /// <summary>
    /// Request body for updating a Memorygram
    /// </summary>
    public class UpdateMemorygramRequest
    {
        [Required(ErrorMessage = "Content is required")]
        [StringLength(10000, ErrorMessage = "Content cannot exceed 10000 characters")]
        public string Content { get; set; } = string.Empty;
        public float[]? VectorEmbedding { get; set; }
    }

    public class CreateMemorygramRequest
    {
        public string Content { get; set; } = string.Empty;
        public float[]? VectorEmbedding { get; set; }
    }

    /// <summary>
    /// Request body for creating an association between Memorygrams
    /// </summary>
    public class CreateAssociationRequest
    {
        /// <summary>
        /// The ID of the target Memorygram to associate with
        /// </summary>
        public Guid TargetId { get; set; }

        /// <summary>
        /// The weight of the association (default: 1.0)
        /// </summary>
        public float Weight { get; set; } = 1.0f;
    }
}
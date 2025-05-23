using System.IO.Abstractions;
using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Persistence;

public class FilePipelinesRepository : IPipelinesRepository
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<FilePipelinesRepository> _logger;

    public FilePipelinesRepository(
        JsonSerializerOptions jsonOptions,
        IFileSystem fileSystem,
        IOptions<PipelineStorageOptions> options,
        ILogger<FilePipelinesRepository> logger)
    {
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        _storagePath = options.Value.StoragePath ?? throw new ArgumentNullException(nameof(options.Value.StoragePath), "StoragePath cannot be null.");
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_fileSystem.Directory.Exists(_storagePath))
        {
            try
            {
                _fileSystem.Directory.CreateDirectory(_storagePath);
                _logger.LogInformation("Created pipeline manifest storage directory at {Path}", _storagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create pipeline manifest storage directory at {Path}", _storagePath);
                throw;
            }
        }
    }

    public async Task<Result<PipelineManifest>> GetPipelineAsync(Guid pipelineId)
    {
        var filePath = _fileSystem.Path.Combine(_storagePath, $"{pipelineId}.json");
        if (!_fileSystem.File.Exists(filePath))
        {
            _logger.LogWarning("Pipeline manifest not found for ID: {PipelineId} at Path: {FilePath}", pipelineId, filePath);
            return Result.Fail<PipelineManifest>($"Pipeline manifest not found: {pipelineId}");
        }

        try
        {
            await using var fileStream = _fileSystem.File.OpenRead(filePath);
            var manifest = await JsonSerializer.DeserializeAsync<PipelineManifest>(fileStream, _jsonOptions);
            if (manifest == null)
            {
                _logger.LogError("Failed to deserialize pipeline manifest for ID: {PipelineId} from Path: {FilePath}. Content was null.", pipelineId, filePath);
                return Result.Fail<PipelineManifest>("Invalid pipeline manifest format: Deserialized content was null.");
            }
            return Result.Ok(manifest);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for pipeline manifest ID: {PipelineId} from Path: {FilePath}", pipelineId, filePath);
            return Result.Fail<PipelineManifest>($"Message deserializing pipeline manifest: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message reading pipeline manifest ID: {PipelineId} from Path: {FilePath}", pipelineId, filePath);
            return Result.Fail<PipelineManifest>($"An unexpected error occurred while reading pipeline manifest: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<PipelineManifest>>> GetAllPipelinesAsync()
    {
        var manifests = new List<PipelineManifest>();
        try
        {
            var files = _fileSystem.Directory.GetFiles(_storagePath, "*.json");
            if (!files.Any())
            {
                _logger.LogInformation("No pipeline manifests found in {Path}", _storagePath);
                return Result.Ok(Enumerable.Empty<PipelineManifest>());
            }

            foreach (var filePath in files)
            {
                try
                {
                    await using var fileStream = _fileSystem.File.OpenRead(filePath);
                    var manifest = await JsonSerializer.DeserializeAsync<PipelineManifest>(fileStream, _jsonOptions);
                    if (manifest != null)
                    {
                        manifests.Add(manifest);
                    }
                    else
                    {
                        _logger.LogWarning("Skipped null manifest during GetAllPipelinesAsync from file: {FilePath}", filePath);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON deserialization error for manifest file: {FilePath} during GetAll. Skipping file.", filePath);
                    // Optionally, collect these errors and return them as part of a partial success.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Message reading manifest file: {FilePath} during GetAll. Skipping file.", filePath);
                }
            }
            return Result.Ok<IEnumerable<PipelineManifest>>(manifests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message retrieving all pipeline manifests from {Path}", _storagePath);
            return Result.Fail<IEnumerable<PipelineManifest>>($"An unexpected error occurred while retrieving all pipeline manifests: {ex.Message}");
        }
    }

    public async Task<Result<PipelineManifest>> CreatePipelineAsync(PipelineManifest manifest)
    {
        if (manifest == null)
        {
            _logger.LogError("Attempted to create a null pipeline manifest.");
            return Result.Fail<PipelineManifest>("Pipeline manifest cannot be null.");
        }

        // Ensure ID is set, or generate if not provided (though typically client might not set it for create)
        if (manifest.Id == Guid.Empty)
        {
            manifest.Id = Guid.NewGuid();
            _logger.LogInformation("Generated new ID {PipelineId} for creating pipeline manifest: {PipelineName}", manifest.Id, manifest.Name);
        }
        else
        {
            _logger.LogInformation("Creating pipeline manifest with provided ID {PipelineId}: {PipelineName}", manifest.Id, manifest.Name);
        }

        var filePath = _fileSystem.Path.Combine(_storagePath, $"{manifest.Id}.json");

        if (_fileSystem.File.Exists(filePath))
        {
            _logger.LogWarning("Pipeline manifest with ID {PipelineId} already exists at {FilePath}. Creation aborted.", manifest.Id, filePath);
            return Result.Fail<PipelineManifest>($"Pipeline manifest with ID {manifest.Id} already exists.");
        }

        try
        {
            await using var fileStream = _fileSystem.File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, manifest, _jsonOptions);
            _logger.LogInformation("Successfully created pipeline manifest ID: {PipelineId} at Path: {FilePath}", manifest.Id, filePath);
            return Result.Ok(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message creating pipeline manifest ID: {PipelineId} at Path: {FilePath}", manifest.Id, filePath);
            return Result.Fail<PipelineManifest>($"An unexpected error occurred while creating pipeline manifest: {ex.Message}");
        }
    }

    public async Task<Result<PipelineManifest>> UpdatePipelineAsync(Guid pipelineId, PipelineManifest manifest)
    {
        if (manifest == null)
        {
            _logger.LogError("Attempted to update pipeline manifest with null data for ID: {PipelineId}", pipelineId);
            return Result.Fail<PipelineManifest>("Pipeline manifest cannot be null for update.");
        }

        if (pipelineId == Guid.Empty)
        {
            _logger.LogError("Attempted to update pipeline manifest with an empty Guid.");
            return Result.Fail<PipelineManifest>("Pipeline ID cannot be empty for update.");
        }

        // Ensure the ID in the manifest matches the pipelineId parameter
        if (manifest.Id != pipelineId)
        {
            _logger.LogWarning("Mismatch between pipelineId parameter ({ParamId}) and manifest.Id ({ManifestId}) during update. Using parameter ID.", pipelineId, manifest.Id);
            manifest.Id = pipelineId; // Ensure consistency
        }

        var filePath = _fileSystem.Path.Combine(_storagePath, $"{pipelineId}.json");
        if (!_fileSystem.File.Exists(filePath))
        {
            _logger.LogWarning("Pipeline manifest not found for update. ID: {PipelineId} at Path: {FilePath}", pipelineId, filePath);
            return Result.Fail<PipelineManifest>($"Pipeline manifest not found for update: {pipelineId}");
        }

        try
        {
            // Overwrite the existing file
            await using var fileStream = _fileSystem.File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, manifest, _jsonOptions);
            _logger.LogInformation("Successfully updated pipeline manifest ID: {PipelineId} at Path: {FilePath}", pipelineId, filePath);
            return Result.Ok(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message updating pipeline manifest ID: {PipelineId} at Path: {FilePath}", pipelineId, filePath);
            return Result.Fail<PipelineManifest>($"An unexpected error occurred while updating pipeline manifest: {ex.Message}");
        }
    }

    public Task<Result> DeletePipelineAsync(Guid pipelineId)
    {
        if (pipelineId == Guid.Empty)
        {
            _logger.LogError("Attempted to delete pipeline manifest with an empty Guid.");
            return Task.FromResult(Result.Fail("Pipeline ID cannot be empty for delete."));
        }

        var filePath = _fileSystem.Path.Combine(_storagePath, $"{pipelineId}.json");
        if (!_fileSystem.File.Exists(filePath))
        {
            _logger.LogWarning("Pipeline manifest not found for deletion. ID: {PipelineId} at Path: {FilePath}", pipelineId, filePath);
            return Task.FromResult(Result.Fail($"Pipeline manifest not found for deletion: {pipelineId}"));
        }

        try
        {
            _fileSystem.File.Delete(filePath);
            _logger.LogInformation("Successfully deleted pipeline manifest ID: {PipelineId} from Path: {FilePath}", pipelineId, filePath);
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message deleting pipeline manifest ID: {PipelineId} from Path: {FilePath}", pipelineId, filePath);
            return Task.FromResult(Result.Fail($"An unexpected error occurred while deleting pipeline manifest: {ex.Message}"));
        }
    }
}

// PipelineStorageOptions class remains the same as in the original file
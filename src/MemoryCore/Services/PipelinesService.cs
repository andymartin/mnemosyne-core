using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models.Pipelines;

namespace Mnemosyne.Core.Services;

public class PipelinesService : IPipelinesService
{
    private readonly IPipelinesRepository _repository;
    private readonly ILogger<PipelinesService> _logger;

    public PipelinesService(
        IPipelinesRepository repository,
        ILogger<PipelinesService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<PipelineManifest>> CreatePipelineAsync(PipelineManifest manifest)
    {
        if (manifest == null)
        {
            _logger.LogError("Cannot create pipeline: manifest is null");
            return Result.Fail<PipelineManifest>("Pipeline manifest cannot be null");
        }

        _logger.LogInformation("Attempting to create pipeline: {PipelineName}", manifest.Name);
        var createResult = await _repository.CreatePipelineAsync(manifest);
        if (createResult.IsFailed)
        {
            _logger.LogError("Failed to create pipeline {PipelineName}: {Errors}", manifest.Name, string.Join(", ", createResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully created pipeline {PipelineName} with ID {PipelineId}", createResult.Value.Name, createResult.Value.Id);
        }
        return createResult;
    }

    public async Task<Result<PipelineManifest>> GetPipelineAsync(Guid pipelineId)
    {
        _logger.LogInformation("Attempting to get pipeline with ID: {PipelineId}", pipelineId);
        var getResult = await _repository.GetPipelineAsync(pipelineId);
        if (getResult.IsFailed)
        {
            _logger.LogWarning("Failed to get pipeline with ID {PipelineId}: {Errors}", pipelineId, string.Join(", ", getResult.Errors.Select(e => e.Message)));
        }
        return getResult;
    }

    public async Task<Result<IEnumerable<PipelineManifest>>> GetAllPipelinesAsync()
    {
        _logger.LogInformation("Attempting to get all pipelines.");
        var getAllResult = await _repository.GetAllPipelinesAsync();
        if (getAllResult.IsFailed)
        {
            _logger.LogError("Failed to get all pipelines: {Errors}", string.Join(", ", getAllResult.Errors.Select(e => e.Message)));
        }
        return getAllResult;
    }

    public async Task<Result<PipelineManifest>> UpdatePipelineAsync(Guid pipelineId, PipelineManifest manifest)
    {
        _logger.LogInformation("Attempting to update pipeline with ID: {PipelineId}", pipelineId);
        var updateResult = await _repository.UpdatePipelineAsync(pipelineId, manifest);
        if (updateResult.IsFailed)
        {
            _logger.LogError("Failed to update pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", updateResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully updated pipeline {PipelineId}", pipelineId);
        }
        return updateResult;
    }

    public async Task<Result> DeletePipelineAsync(Guid pipelineId)
    {
        _logger.LogInformation("Attempting to delete pipeline with ID: {PipelineId}", pipelineId);
        var deleteResult = await _repository.DeletePipelineAsync(pipelineId);
        if (deleteResult.IsFailed)
        {
            _logger.LogError("Failed to delete pipeline {PipelineId}: {Errors}", pipelineId, string.Join(", ", deleteResult.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Successfully deleted pipeline {PipelineId}", pipelineId);
        }
        return deleteResult;
    }
}

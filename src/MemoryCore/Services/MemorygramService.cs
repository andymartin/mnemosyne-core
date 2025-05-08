using System;
using System.Threading.Tasks;
using MemoryCore.Models;
using MemoryCore.Interfaces;
using FluentResults;

namespace MemoryCore.Services
{
    public class MemorygramService : IMemorygramService
    {
        private readonly IMemorygramRepository _repository;
        private readonly ILogger<MemorygramService> _logger;

        public MemorygramService(IMemorygramRepository repository, ILogger<MemorygramService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram)
        {
            try
            {
                return await _repository.CreateOrUpdateMemorygramAsync(memorygram);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating or updating memorygram");
                return Result.Fail<Memorygram>($"Service error: {ex.Message}");
            }
        }

        public async Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight)
        {
            try
            {
                return await _repository.CreateAssociationAsync(fromId, toId, weight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating association between {FromId} and {ToId}", fromId, toId);
                return Result.Fail($"Service error: {ex.Message}");
            }
        }

        public async Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id)
        {
            try
            {
                return await _repository.GetMemorygramByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving memorygram by ID {Id}", id);
                return Result.Fail<Memorygram>($"Service error: {ex.Message}");
            }
        }
    }

    public interface IMemorygramService
    {
        Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram);
        Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight);
        Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id);
    }
}

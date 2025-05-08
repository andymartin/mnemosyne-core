using FluentResults;
using MemoryCore.Models;

namespace MemoryCore.Interfaces
{
    public interface IMemorygramRepository
    {
        Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram);
        Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight);
        Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id);
    }
}

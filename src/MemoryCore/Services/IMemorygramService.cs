using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public interface IMemorygramService
{
    Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram);
    Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight);
    Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id);
}
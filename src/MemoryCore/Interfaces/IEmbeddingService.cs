using FluentResults;

namespace Mnemosyne.Core.Interfaces;

public interface IEmbeddingService
{
    Task<Result<float[]>> GetEmbeddingAsync(string text);
}
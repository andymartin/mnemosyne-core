using System.Threading.Tasks;
using FluentResults;

namespace MemoryCore.Interfaces
{
    public interface IEmbeddingService
    {
        Task<Result<float[]>> GetEmbeddingAsync(string text);
    }
}
using System.Threading.Tasks;
using FluentResults;
using MemoryCore.Interfaces;

namespace MemoryCore.Tests.Integration
{
    public class MockEmbeddingService : IEmbeddingService
    {
        public Task<Result<float[]>> GetEmbeddingAsync(string text)
        {
            // Return a predictable, fixed-size embedding for any input text.
            // The actual values don't matter much for most integration tests,
            // as long as they are consistent and have the expected dimension.
            var mockEmbedding = new float[1024]; // Example dimension
            for (int i = 0; i < mockEmbedding.Length; i++)
            {
                mockEmbedding[i] = (float)i / 1000; // Arbitrary predictable values
            }
            return Task.FromResult(Result.Ok(mockEmbedding));
        }
    }
}
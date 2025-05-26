using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

/// <summary>
/// Mock implementation of ICognitiveProcessingService for MVP development
/// This will be replaced with the actual CPP service integration later
/// </summary>
public class MockCognitiveProcessingService : ICognitiveProcessingService
{
    private readonly ILogger<MockCognitiveProcessingService> _logger;

    public MockCognitiveProcessingService(ILogger<MockCognitiveProcessingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CognitiveProcessingResult>> ProcessAsync(
        string userText,
        IEnumerable<Memorygram> threadHistory,
        IEnumerable<Memorygram> associativeMemories)
    {
        try
        {
            _logger.LogInformation("Mock CPP processing for user text: {UserText}", userText);

            // Simulate some processing delay
            await Task.Delay(100);

            // Create a mock response
            var mockResponse = $"Mock response to: {userText}. This is a placeholder response from the mock CPP service.";

            // Extract some memory IDs that were "utilized" (for demo purposes)
            var utilizedMemoryIds = threadHistory
                .Take(2)
                .Concat(associativeMemories.Take(2))
                .Select(m => m.Id)
                .ToList();

            var result = new CognitiveProcessingResult
            {
                ResponseText = mockResponse,
                UtilizedMemoryIds = utilizedMemoryIds
            };

            _logger.LogInformation("Mock CPP processing completed, utilized {MemoryCount} memories", 
                result.UtilizedMemoryIds.Count);

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in mock CPP processing");
            return Result.Fail(new Error($"Mock CPP processing error: {ex.Message}"));
        }
    }
}
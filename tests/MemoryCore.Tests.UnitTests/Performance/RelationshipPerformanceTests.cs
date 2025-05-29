using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using NSubstitute;
using Shouldly;
using System.Diagnostics;
using Xunit.Abstractions;

namespace MemoryCore.Tests.UnitTests.Performance;

public class RelationshipPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IMemorygramRepository _repositoryMock;
    private readonly IEmbeddingService _embeddingServiceMock;
    private readonly ISemanticReformulator _semanticReformulatorMock;
    private readonly ILogger<MemorygramService> _loggerMock;
    private readonly MemorygramService _service;

    public RelationshipPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _repositoryMock = Substitute.For<IMemorygramRepository>();
        _embeddingServiceMock = Substitute.For<IEmbeddingService>();
        _semanticReformulatorMock = Substitute.For<ISemanticReformulator>();
        _loggerMock = Substitute.For<ILogger<MemorygramService>>();

        _service = new MemorygramService(
            _repositoryMock,
            _embeddingServiceMock,
            _semanticReformulatorMock,
            _loggerMock);
    }

    [Fact]
    public async Task CreateRelationshipAsync_Performance_ShouldCompleteWithin100ms()
    {
        // Arrange
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var relationshipType = "PERFORMANCE_TEST";
        var weight = 0.85f;

        var expectedRelationship = new GraphRelationship(
            Guid.NewGuid(),
            fromId,
            toId,
            relationshipType,
            weight,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        _repositoryMock
            .CreateRelationshipAsync(fromId, toId, relationshipType, weight, Arg.Any<string>())
            .Returns(Result.Ok(expectedRelationship));

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.CreateRelationshipAsync(fromId, toId, relationshipType, weight);
        stopwatch.Stop();

        _output.WriteLine($"CreateRelationshipAsync took {stopwatch.ElapsedMilliseconds}ms");

        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task GetRelationshipsByMemorygramIdAsync_Performance_ShouldCompleteWithin100ms()
    {
        // Arrange
        var memorygramId = Guid.NewGuid();
        var relationships = GenerateTestRelationships(memorygramId, 50);

        _repositoryMock
            .GetRelationshipsByMemorygramIdAsync(memorygramId, true, true)
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.GetRelationshipsByMemorygramIdAsync(memorygramId);
        stopwatch.Stop();

        _output.WriteLine($"GetRelationshipsByMemorygramIdAsync with 50 relationships took {stopwatch.ElapsedMilliseconds}ms");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count().ShouldBe(50);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task FindRelationshipsAsync_Performance_ShouldCompleteWithin200ms()
    {
        // Arrange
        var relationships = GenerateTestRelationships(Guid.NewGuid(), 100);

        _repositoryMock
            .FindRelationshipsAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<float?>(), Arg.Any<float?>(), Arg.Any<bool?>())
            .Returns(Result.Ok<IEnumerable<GraphRelationship>>(relationships));

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.FindRelationshipsAsync(minWeight: 0.5f, maxWeight: 1.0f);
        stopwatch.Stop();

        _output.WriteLine($"FindRelationshipsAsync with 100 relationships took {stopwatch.ElapsedMilliseconds}ms");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count().ShouldBe(100);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200);
    }

    [Fact]
    public async Task BulkRelationshipOperations_Performance_ShouldCompleteWithin500ms()
    {
        // Arrange
        var memorygramIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        var relationshipType = "BULK_TEST";

        _repositoryMock
            .CreateRelationshipAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<float>(), Arg.Any<string>())
            .Returns(callInfo => Result.Ok(new GraphRelationship(
                Guid.NewGuid(),
                callInfo.ArgAt<Guid>(0),
                callInfo.ArgAt<Guid>(1),
                callInfo.ArgAt<string>(2),
                callInfo.ArgAt<float>(3),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            )));

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task<Result<GraphRelationship>>>();

        // Create relationships between each pair of memorygrams
        for (int i = 0; i < memorygramIds.Count; i++)
        {
            for (int j = i + 1; j < memorygramIds.Count; j++)
            {
                tasks.Add(_service.CreateRelationshipAsync(
                    memorygramIds[i], 
                    memorygramIds[j], 
                    relationshipType, 
                    0.5f + (float)(i + j) / 100));
            }
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        _output.WriteLine($"Created {results.Length} relationships in {stopwatch.ElapsedMilliseconds}ms");

        results.ShouldAllBe(r => r.IsSuccess);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500);
    }

    private static List<GraphRelationship> GenerateTestRelationships(Guid centralMemorygramId, int count)
    {
        var relationships = new List<GraphRelationship>();
        var random = new Random(42); // Seed for reproducible results

        for (int i = 0; i < count; i++)
        {
            var isOutgoing = i % 2 == 0;
            var fromId = isOutgoing ? centralMemorygramId : Guid.NewGuid();
            var toId = isOutgoing ? Guid.NewGuid() : centralMemorygramId;

            relationships.Add(new GraphRelationship(
                Guid.NewGuid(),
                fromId,
                toId,
                $"PERF_TEST_{i % 5}",
                (float)random.NextDouble(),
                DateTimeOffset.UtcNow.AddMinutes(-i),
                DateTimeOffset.UtcNow.AddMinutes(-i),
                i % 3 == 0 ? $"{{\"index\": {i}}}" : null,
                true
            ));
        }

        return relationships;
    }
}
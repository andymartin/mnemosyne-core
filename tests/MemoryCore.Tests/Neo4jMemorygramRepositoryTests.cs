using FluentResults;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Persistence;
using Neo4j.Driver;
using NSubstitute;
using Shouldly;

namespace Mnemosyne.Core.Tests
{
    public class Neo4jMemorygramRepositoryTests
    {
        private readonly IMemorygramRepository _repository;
        private readonly IDriver _driver;
        private readonly IAsyncSession _session;
        private readonly IAsyncQueryRunner _queryRunner;
        private readonly ILogger<Neo4jMemorygramRepository> _logger;

        public Neo4jMemorygramRepositoryTests()
        {
            _driver = Substitute.For<IDriver>();
            _session = Substitute.For<IAsyncSession>();
            _queryRunner = Substitute.For<IAsyncQueryRunner>();
            _logger = Substitute.For<ILogger<Neo4jMemorygramRepository>>();
            
            _driver.AsyncSession().Returns(_session);
            
            var testGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var mockRecord = Substitute.For<IRecord>();
            mockRecord["m.id"].Returns(testGuid.ToString());
            mockRecord["m.content"].Returns("Test Content");
            mockRecord["m.vectorEmbedding"].Returns(new float[] { 0.1f, 0.2f });
            mockRecord["m.createdAt"].Returns(DateTimeOffset.UtcNow);
            mockRecord["m.updatedAt"].Returns(DateTimeOffset.UtcNow);
            
            var mockCursor = Substitute.For<IResultCursor>();
            mockCursor.FetchAsync().Returns(true, false);
            mockCursor.Current.Returns(mockRecord);
            
            _queryRunner.RunAsync(Arg.Any<string>(), Arg.Any<object>())
                .Returns(Task.FromResult(mockCursor));
            
            _session.ExecuteWriteAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<object>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>())
                .Returns(callInfo =>
                {
                    var func = callInfo.ArgAt<Func<IAsyncQueryRunner, Task<object>>>(0);
                    return func(_queryRunner);
                });

            _repository = new Neo4jMemorygramRepository(_driver, _logger);
        }

        [Fact]
        public async Task CreateOrUpdateMemorygramAsync_ShouldPassCorrectParameters()
        {
            // Arrange
            var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var memorygram = new Memorygram(
                id,
                "Test Content",
                new float[] { 0.1f, 0.2f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            var recordValues = new Dictionary<string, object>
            {
                ["m.id"] = memorygram.Id.ToString(),
                ["m.content"] = memorygram.Content,
                ["m.vectorEmbedding"] = memorygram.VectorEmbedding,
                ["m.createdAt"] = memorygram.CreatedAt,
                ["m.updatedAt"] = memorygram.UpdatedAt,
            };

            var logger = Substitute.For<ILogger<Neo4jMemorygramRepository>>();
            var session = Substitute.For<IAsyncSession>();
            var driver = Substitute.For<IDriver>();
            driver.AsyncSession().Returns(session);

            var mockRecord = Substitute.For<IRecord>();
            mockRecord["m.id"].Returns(memorygram.Id.ToString());
            mockRecord["m.content"].Returns(memorygram.Content);
            mockRecord["m.vectorEmbedding"].Returns(memorygram.VectorEmbedding);
            mockRecord["m.createdAt"].Returns(memorygram.CreatedAt);
            mockRecord["m.updatedAt"].Returns(memorygram.UpdatedAt);
            mockRecord.Values.Returns(recordValues);

            var mockCursor = Substitute.For<IResultCursor>();
            mockCursor.FetchAsync().Returns(true);
            mockCursor.Current.Returns(mockRecord);

            _queryRunner.RunAsync(Arg.Any<string>(), Arg.Any<object>())
                .Returns(Task.FromResult(mockCursor));

            _session.ClearReceivedCalls();
            _session.ExecuteWriteAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>())
                .Returns(callInfo =>
                {
                    var func = callInfo.ArgAt<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(0);
                    return func(_queryRunner);
                });

            // Act
            var result = await _repository.CreateOrUpdateMemorygramAsync(memorygram);

            // Assert
            await _session.Received(1).ExecuteWriteAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>());

            await _queryRunner.Received(1).RunAsync(
                Arg.Is<string>(s => s.Contains("MATCH")),
                Arg.Is<object>(o => HasProperty(o, "id", id.ToString())));

            result.IsSuccess.ShouldBeTrue();
        }

        [Fact]
        public async Task CreateAssociationAsync_ShouldPassCorrectParameters()
        {
            // Arrange
            var fromId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var toId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var weight = 0.75f;

            var memorygram = new Memorygram(
                fromId,
                "Test Content",
                new float[] { 0.1f, 0.2f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            var recordValues = new Dictionary<string, object>
            {
                ["id"] = memorygram.Id.ToString(),
                ["content"] = memorygram.Content,
                ["vectorEmbedding"] = memorygram.VectorEmbedding,
                ["createdAt"] = memorygram.CreatedAt,
                ["updatedAt"] = memorygram.UpdatedAt,
            };

            var mockRecord = Substitute.For<IRecord>();
            mockRecord["id"].Returns(memorygram.Id.ToString());
            mockRecord["content"].Returns(memorygram.Content);
            mockRecord["vectorEmbedding"].Returns(memorygram.VectorEmbedding);
            mockRecord["createdAt"].Returns(memorygram.CreatedAt);
            mockRecord["updatedAt"].Returns(memorygram.UpdatedAt);
            mockRecord.Values.Returns(recordValues);

            var mockCursor = Substitute.For<IResultCursor>();
            mockCursor.FetchAsync().Returns(true);
            mockCursor.Current.Returns(mockRecord);

            _queryRunner.RunAsync(Arg.Any<string>(), Arg.Any<object>())
                .Returns(Task.FromResult(mockCursor));

            _session.ClearReceivedCalls();
            _session.ExecuteWriteAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>())
                .Returns(callInfo =>
                {
                    var func = callInfo.ArgAt<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(0);
                    return func(_queryRunner);
                });

            // Act
            var result = await _repository.CreateAssociationAsync(fromId, toId, weight);

            // Assert
            await _session.Received(1).ExecuteWriteAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>());

            await _queryRunner.Received(1).RunAsync(
                Arg.Is<string>(s => s.Contains("MERGE")),
                Arg.Is<object>(o => HasProperty(o, "fromId", fromId.ToString()) &&
                                    HasProperty(o, "toId", toId.ToString()) &&
                                    HasProperty(o, "weight", weight)));

            result.IsSuccess.ShouldBeTrue();
        }

        [Fact]
        public async Task GetMemorygramByIdAsync_ShouldReturnMemorygram()
        {
            // Arrange
            var id = Guid.Parse("00000000-0000-0000-0000-000000000001");            
            var mockRecord = Substitute.For<IRecord>();
            
            // Create a dictionary to simulate record.Values
            var recordValues = new Dictionary<string, object>
            {
                ["id"] = id.ToString(),
                ["content"] = "Test Content",
                ["vectorEmbedding"] = new float[] { 0.1f, 0.2f },
                ["createdAt"] = DateTimeOffset.UtcNow,
                ["updatedAt"] = DateTimeOffset.UtcNow
            };
            
            mockRecord["id"].Returns(id.ToString());
            mockRecord["content"].Returns("Test Content");
            mockRecord["vectorEmbedding"].Returns(new float[] { 0.1f, 0.2f });
            mockRecord["createdAt"].Returns(DateTimeOffset.UtcNow);
            mockRecord["updatedAt"].Returns(DateTimeOffset.UtcNow);
            mockRecord.Values.Returns(recordValues);
            
            var mockCursor = Substitute.For<IResultCursor>();
            mockCursor.FetchAsync().Returns(true);
            mockCursor.Current.Returns(mockRecord);
            
            _queryRunner.RunAsync(
                Arg.Is<string>(s => s.Contains("MATCH")),
                Arg.Is<object>(o => HasProperty(o, "id", id.ToString())))
                .Returns(Task.FromResult(mockCursor));
            
            _session.ClearReceivedCalls();
            _session.ExecuteReadAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>())
                .Returns(callInfo =>
                {
                    var func = callInfo.ArgAt<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(0);
                    return func(_queryRunner);
                });
            
            // Act
            var result = await _repository.GetMemorygramByIdAsync(id);
            
            // Assert
            result.Errors.ShouldBeEmpty();
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Id.ShouldBe(id);

            await _session.Received(1).ExecuteReadAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>());
            
            await _queryRunner.Received(1).RunAsync(
                Arg.Is<string>(s => s.Contains("MATCH")),
                Arg.Is<object>(o => HasProperty(o, "id", id.ToString())));
        }

        [Fact]
        public async Task GetMemorygramByIdAsync_ShouldReturnNotFound()
        {
            // Arrange
            var id = Guid.Parse("00000000-0000-0000-0000-000000000099");
            
            var emptyCursor = Substitute.For<IResultCursor>();
            emptyCursor.FetchAsync().Returns(false);
            
            _queryRunner.RunAsync(
                Arg.Is<string>(s => s.Contains("MATCH")),
                Arg.Is<object>(o => HasProperty(o, "id", id.ToString())))
                .Returns(Task.FromResult(emptyCursor));
            
            _session.ClearReceivedCalls();
            _session.ExecuteReadAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>())
                .Returns(callInfo =>
                {
                    var func = callInfo.ArgAt<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(0);
                    return func(_queryRunner);
                });
            
            // Act
            var result = await _repository.GetMemorygramByIdAsync(id);
            
            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldNotBeEmpty();
            result.Errors[0].Message.ShouldContain("not found", Case.Insensitive);

            await _session.Received(1).ExecuteReadAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>());

            await _queryRunner.Received(1).RunAsync(
                Arg.Is<string>(s => s.Contains("MATCH")),
                Arg.Is<object>(o => HasProperty(o, "id", id.ToString())));
        }

        [Fact]
        public async Task CreateOrUpdateMemorygramAsync_ShouldHandleDatabaseError()
        {
            // Arrange
            var testGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var memorygram = new Memorygram(
                testGuid,
                "Test Content",
                new float[] { 0.1f, 0.2f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );
            
            // Setup session to throw exception for this test
            _session.ExecuteWriteAsync(
                Arg.Any<Func<IAsyncQueryRunner, Task<Result<Memorygram>>>>(),
                Arg.Any<Action<TransactionConfigBuilder>>())
                    .Returns<Task<Result<Memorygram>>>(x => throw new Exception("Database error"));
            
            // Act
            var result = await _repository.CreateOrUpdateMemorygramAsync(memorygram);
            
            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors[0].Message.ShouldContain("Database error");
        }
        
        // Helper method to check if an object has a property with a specific value
        private bool HasProperty(object obj, string propertyName, object expectedValue)
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property == null)
                return false;
                
            var value = property.GetValue(obj);
            return value != null && value.Equals(expectedValue);
        }
    }
}

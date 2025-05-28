using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Controllers;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using NSubstitute;
using Shouldly;
using Xunit.Abstractions;

namespace MemoryCore.Tests.UnitTests.Controllers;

public class MemorygramsControllerTests
{
    private readonly IMemorygramService _memorygramService;
    private readonly ILogger<MemorygramsController> _logger;
    private readonly MemorygramsController _controller;
    private readonly ITestOutputHelper _output;

    public MemorygramsControllerTests(ITestOutputHelper output)
    {
        _output = output;
        _memorygramService = Substitute.For<IMemorygramService>();
        _logger = Substitute.For<ILogger<MemorygramsController>>();
        _controller = new MemorygramsController(_memorygramService, _logger);
    }

    [Fact]
    public async Task CreateMemorygram_WithValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new CreateMemorygramRequest
        {
            Content = "Test content",
            Type = MemorygramType.UserInput
        };

        var expectedGuid = Guid.NewGuid();

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(callInfo =>
            {
                var arg = callInfo.ArgAt<Memorygram>(0);
                return Result.Ok(new Memorygram(
                    expectedGuid,
                    arg.Content,
                    arg.Type,
                    arg.TopicalEmbedding,
                    arg.ContentEmbedding,
                    arg.ContextEmbedding,
                    arg.MetadataEmbedding,
                    arg.Source,
                    arg.Timestamp,
                    arg.CreatedAt,
                    arg.UpdatedAt,
                    arg.ChatId,
                    arg.PreviousMemorygramId,
                    arg.NextMemorygramId,
                    arg.Sequence
                ));
            });

        var expectedMemorygram = new Memorygram(
            expectedGuid,
            request.Content,
            request.Type,
            Array.Empty<float>(), // TopicalEmbedding
            Array.Empty<float>(), // ContentEmbedding
            Array.Empty<float>(), // ContextEmbedding
            Array.Empty<float>(), // MetadataEmbedding
            "User",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null
        );

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Ok(expectedMemorygram));

        // Act
        var result = await _controller.CreateMemorygram(request);

        // Assert
        result.ShouldBeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result;
        createdResult.StatusCode.ShouldBe(StatusCodes.Status201Created);
        createdResult.ActionName.ShouldBe(nameof(MemorygramsController.GetMemorygram));
        createdResult.RouteValues!["id"].ShouldBe(expectedGuid);
        createdResult.Value.ShouldBe(expectedMemorygram);

        await _memorygramService.Received(1).CreateOrUpdateMemorygramAsync(Arg.Is<Memorygram>(m =>
            m.Content == request.Content &&
            m.Source == "User" &&
            m.Timestamp > 0));
    }

    [Fact]
    public async Task CreateMemorygram_WithEmptyContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMemorygramRequest
        {
            Content = ""
        };

        // Act
        var result = await _controller.CreateMemorygram(request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequestResult.Value.ShouldBe("Content is required");

        await _memorygramService.DidNotReceive().CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>());
    }

    [Fact]
    public async Task CreateMemorygram_WithNullEmbeddings_UsesEmptyArrays()
    {
        // Arrange
        var request = new CreateMemorygramRequest
        {
            Content = "Test content",
            Type = MemorygramType.UserInput
        };

        var expectedGuid = Guid.NewGuid();

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(callInfo =>
            {
                var arg = callInfo.ArgAt<Memorygram>(0);
                return Result.Ok(new Memorygram(
                    expectedGuid,
                    arg.Content,
                    arg.Type,
                    arg.TopicalEmbedding,
                    arg.ContentEmbedding,
                    arg.ContextEmbedding,
                    arg.MetadataEmbedding,
                    arg.Source,
                    arg.Timestamp,
                    arg.CreatedAt,
                    arg.UpdatedAt,
                    arg.ChatId,
                    arg.PreviousMemorygramId,
                    arg.NextMemorygramId,
                    arg.Sequence
                ));
            });

        // Act
        var result = await _controller.CreateMemorygram(request);

        // Assert
        result.ShouldBeOfType<CreatedAtActionResult>();

        await _memorygramService.Received(1).CreateOrUpdateMemorygramAsync(Arg.Is<Memorygram>(m =>
            m.Content == request.Content &&
            m.TopicalEmbedding.Length == 0 &&
            m.ContentEmbedding.Length == 0 &&
            m.ContextEmbedding.Length == 0 &&
            m.MetadataEmbedding.Length == 0));
    }

    [Fact]
    public async Task CreateMemorygram_WhenServiceFails_ReturnsInternalServerError()
    {
        // Arrange
        var request = new CreateMemorygramRequest
        {
            Content = "Test content",
            Type = MemorygramType.UserInput
        };

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Fail<Memorygram>("Database error"));

        // Act
        var result = await _controller.CreateMemorygram(request);

        // Assert
        result.ShouldBeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task GetMemorygram_WithExistingId_ReturnsOkResult()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var expectedMemorygram = new Memorygram(
            id,
            "Test content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "TestSource",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Ok(expectedMemorygram));

        // Act
        var result = await _controller.GetMemorygram(id);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        okResult.Value.ShouldBe(expectedMemorygram);

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
    }

    [Fact]
    public async Task GetMemorygram_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000099"); // Non-existing ID

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Fail<Memorygram>($"Memorygram with ID {id} not found"));

        // Act
        var result = await _controller.GetMemorygram(id);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFoundResult.Value.ShouldBe($"Memorygram with ID {id} not found");

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
    }

    [Fact]
    public async Task GetMemorygram_WhenServiceFails_ReturnsInternalServerError()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Fail<Memorygram>("Database error"));

        // Act
        var result = await _controller.GetMemorygram(id);

        // Assert
        result.ShouldBeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task UpdateMemorygram_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest
        {
            Content = "Updated content",
            Type = MemorygramType.UserInput
        };

        var existingMemorygram = new Memorygram(
            guidId,
            "Original content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "OriginalSource",
            DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            null,
            null,
            null
        );

        var updatedMemorygram = new Memorygram(
            guidId,
            request.Content,
            request.Type,
            existingMemorygram.TopicalEmbedding,
            existingMemorygram.ContentEmbedding,
            existingMemorygram.ContextEmbedding,
            existingMemorygram.MetadataEmbedding,
            existingMemorygram.Source,
            existingMemorygram.Timestamp,
            existingMemorygram.CreatedAt,
            DateTimeOffset.UtcNow,
            existingMemorygram.ChatId,
            existingMemorygram.PreviousMemorygramId,
            existingMemorygram.NextMemorygramId,
            existingMemorygram.Sequence
        );

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Ok(existingMemorygram));

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Ok(updatedMemorygram));

        // Act
        var result = await _controller.UpdateMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        okResult.Value.ShouldBe(updatedMemorygram);

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
        await _memorygramService.Received(1).CreateOrUpdateMemorygramAsync(Arg.Is<Memorygram>(m =>
            m.Id == guidId &&
            m.Content == request.Content &&
            m.Source == existingMemorygram.Source &&
            m.Timestamp == existingMemorygram.Timestamp));
    }

    [Fact]
    public async Task UpdateMemorygram_WithEmptyId_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.Empty;
        var request = new UpdateMemorygramRequest
        {
            Content = "Updated content",
            Type = MemorygramType.UserInput
        };

        // Act
        var result = await _controller.UpdateMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequestResult.Value.ShouldBe("Memorygram ID is required in the route.");

        await _memorygramService.DidNotReceive().GetMemorygramByIdAsync(Arg.Any<Guid>());
        await _memorygramService.DidNotReceive().CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>());
    }

    [Fact]
    public async Task UpdateMemorygram_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest
        {
            Content = "Updated content",
            Type = MemorygramType.UserInput
        };

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Fail<Memorygram>($"Memorygram with ID {id} not found"));

        // Act
        var result = await _controller.UpdateMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFoundResult.Value.ShouldBe($"Memorygram with ID {id} not found");

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
        await _memorygramService.DidNotReceive().CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>());
    }

    [Fact]
    public async Task UpdateMemorygram_WithNoUpdateParameters_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest();

        var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var existingMemorygram = new Memorygram(
            guidId,
            "Original content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "OriginalSource",
            DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Ok(existingMemorygram));

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Ok(existingMemorygram));

        // Act
        var result = await _controller.UpdateMemorygram(id, request);

        // Assert
        _logger.LogInformation("Actual result type: {Type}", result.GetType().FullName);
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        var errorObj = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value));

        errorObj.ShouldNotBeNull();
        errorObj["Error"].ShouldBe("InvalidRequest");
        errorObj["Message"].ShouldBe("Content must be provided and cannot be empty");

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
        await _memorygramService.DidNotReceive().CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>());
    }

    [Fact]
    public async Task UpdateMemorygram_WhenServiceFails_ReturnsInternalServerError()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest
        {
            Content = "Updated content",
            Type = MemorygramType.UserInput
        };

        var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var existingMemorygram = new Memorygram(
            guidId,
            "Original content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "OriginalSource",
            DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Ok(existingMemorygram));

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Fail<Memorygram>("Database error"));

        // Act
        var result = await _controller.UpdateMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task PatchMemorygram_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest
        {
            Content = "Patched content",
            Type = MemorygramType.UserInput
        };

        var existingMemorygram = new Memorygram(
            guidId,
            "Original content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "OriginalSource",
            DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            null,
            null,
            null
        );

        var patchedMemorygram = new Memorygram(
            guidId,
            request.Content,
            request.Type,
            existingMemorygram.TopicalEmbedding,
            existingMemorygram.ContentEmbedding,
            existingMemorygram.ContextEmbedding,
            existingMemorygram.MetadataEmbedding,
            existingMemorygram.Source,
            existingMemorygram.Timestamp,
            existingMemorygram.CreatedAt,
            DateTimeOffset.UtcNow,
            existingMemorygram.ChatId,
            existingMemorygram.PreviousMemorygramId,
            existingMemorygram.NextMemorygramId,
            existingMemorygram.Sequence
        );

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Ok(existingMemorygram));

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Ok(patchedMemorygram));

        // Act
        var result = await _controller.PatchMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        okResult.Value.ShouldBe(patchedMemorygram);

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
        await _memorygramService.Received(1).CreateOrUpdateMemorygramAsync(Arg.Is<Memorygram>(m =>
            m.Id == guidId &&
            m.Content == request.Content &&
            m.TopicalEmbedding == existingMemorygram.TopicalEmbedding &&
            m.ContentEmbedding == existingMemorygram.ContentEmbedding &&
            m.ContextEmbedding == existingMemorygram.ContextEmbedding &&
            m.MetadataEmbedding == existingMemorygram.MetadataEmbedding &&
            m.Source == existingMemorygram.Source &&
            m.Timestamp == existingMemorygram.Timestamp));
    }

    [Fact]
    public async Task PatchMemorygram_WithEmptyId_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.Empty;
        var request = new UpdateMemorygramRequest
        {
            Content = "Patched content",
            Type = MemorygramType.UserInput
        };

        // Act
        var result = await _controller.PatchMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequestResult.Value.ShouldBe("Memorygram ID is required in the route.");

        await _memorygramService.DidNotReceive().GetMemorygramByIdAsync(Arg.Any<Guid>());
        await _memorygramService.DidNotReceive().CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>());
    }

    [Fact]
    public async Task PatchMemorygram_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest
        {
            Content = "Patched content",
            Type = MemorygramType.UserInput
        };

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Fail<Memorygram>($"Memorygram with ID {id} not found"));

        // Act
        var result = await _controller.PatchMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFoundResult.Value.ShouldBe($"Memorygram with ID {id} not found");

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
        await _memorygramService.DidNotReceive().CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>());
    }

    [Fact]
    public async Task PatchMemorygram_WithNoUpdateParameters_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest();

        var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var existingMemorygram = new Memorygram(
            guidId,
            "Original content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "OriginalSource",
            DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Ok(existingMemorygram));

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Ok(existingMemorygram));

        // Act
        var result = await _controller.UpdateMemorygram(id, request);

        // Assert
        _logger.LogInformation("Actual result type: {Type}", result.GetType().FullName);
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        var errorObj = JsonSerializer.Deserialize<Dictionary<string, string>>(
            JsonSerializer.Serialize(badRequestResult.Value));

        errorObj.ShouldNotBeNull();
        errorObj["Error"].ShouldBe("InvalidRequest");
        errorObj["Message"].ShouldBe("Content must be provided and cannot be empty");

        await _memorygramService.Received(1).GetMemorygramByIdAsync(id);
        await _memorygramService.DidNotReceive().CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>());
    }

    [Fact]
    public async Task PatchMemorygram_WhenServiceFails_ReturnsInternalServerError()
    {
        // Arrange
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new UpdateMemorygramRequest
        {
            Content = "Patched content",
            Type = MemorygramType.UserInput
        };

        var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var existingMemorygram = new Memorygram(
            guidId,
            "Original content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "OriginalSource",
            DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(id)
            .Returns(Result.Ok(existingMemorygram));

        _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
            .Returns(Result.Fail<Memorygram>("Database error"));

        // Act
        var result = await _controller.PatchMemorygram(id, request);

        // Assert
        result.ShouldBeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task CreateAssociation_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var sourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var targetId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var request = new CreateAssociationRequest
        {
            TargetId = targetId,
            Weight = 0.5f
        };

        var sourceMemorygram = new Memorygram(
            sourceId,
            "Source content",
            MemorygramType.UserInput,
            Array.Empty<float>(), // TopicalEmbedding
            Array.Empty<float>(), // ContentEmbedding
            Array.Empty<float>(), // ContextEmbedding
            Array.Empty<float>(), // MetadataEmbedding
            "Source",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null
        );

        var targetMemorygram = new Memorygram(
            targetId,
            "Target content",
            MemorygramType.UserInput,
            Array.Empty<float>(), // TopicalEmbedding
            Array.Empty<float>(), // ContentEmbedding
            Array.Empty<float>(), // ContextEmbedding
            Array.Empty<float>(), // MetadataEmbedding
            "Target",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(sourceId)
            .Returns(Result.Ok(sourceMemorygram));
        _memorygramService.GetMemorygramByIdAsync(targetId)
            .Returns(Result.Ok(targetMemorygram));
        _memorygramService.CreateAssociationAsync(sourceId, targetId, request.Weight)
            .Returns(Result.Ok(sourceMemorygram));

        // Act
        var result = await _controller.CreateAssociation(sourceId, request);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        okResult.Value.ShouldBe(sourceMemorygram);

        await _memorygramService.Received(1).GetMemorygramByIdAsync(sourceId);
        await _memorygramService.Received(1).GetMemorygramByIdAsync(targetId);
        await _memorygramService.Received(1).CreateAssociationAsync(sourceId, targetId, request.Weight);
    }

    [Fact]
    public async Task CreateAssociation_WithEmptySourceId_ReturnsBadRequest()
    {
        // Arrange
        var sourceId = Guid.Empty;
        var request = new CreateAssociationRequest
        {
            TargetId = Guid.NewGuid(),
            Weight = 0.5f
        };

        // Act
        var result = await _controller.CreateAssociation(sourceId, request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequestResult.Value.ShouldBe("Source Memorygram ID is required in the route.");

        await _memorygramService.DidNotReceive().GetMemorygramByIdAsync(Arg.Any<Guid>());
        await _memorygramService.DidNotReceive().CreateAssociationAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<float>());
    }

    [Fact]
    public async Task CreateAssociation_WithEmptyTargetId_ReturnsBadRequest()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var request = new CreateAssociationRequest
        {
            TargetId = Guid.Empty,
            Weight = 0.5f
        };

        // Act
        var result = await _controller.CreateAssociation(sourceId, request);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequestResult.Value.ShouldBe("Target Memorygram ID is required in the request body.");

        await _memorygramService.DidNotReceive().GetMemorygramByIdAsync(Arg.Any<Guid>());
        await _memorygramService.DidNotReceive().CreateAssociationAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<float>());
    }

    [Fact]
    public async Task CreateAssociation_WithNonExistingSourceId_ReturnsNotFound()
    {
        // Arrange
        var sourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var targetId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var request = new CreateAssociationRequest
        {
            TargetId = targetId,
            Weight = 0.5f
        };

        _memorygramService.GetMemorygramByIdAsync(sourceId)
            .Returns(Result.Fail<Memorygram>($"Memorygram with ID {sourceId} not found"));

        // Act
        var result = await _controller.CreateAssociation(sourceId, request);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFoundResult.Value.ShouldBe($"Source Memorygram with ID {sourceId} not found");

        await _memorygramService.Received(1).GetMemorygramByIdAsync(sourceId);
        await _memorygramService.DidNotReceive().GetMemorygramByIdAsync(targetId);
        await _memorygramService.DidNotReceive().CreateAssociationAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<float>());
    }

    [Fact]
    public async Task CreateAssociation_WithNonExistingTargetId_ReturnsNotFound()
    {
        // Arrange
        var sourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var targetId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var request = new CreateAssociationRequest
        {
            TargetId = targetId,
            Weight = 0.5f
        };

        var sourceMemorygram = new Memorygram(
            sourceId,
            "Source content",
            MemorygramType.UserInput,
            Array.Empty<float>(), // TopicalEmbedding
            Array.Empty<float>(), // ContentEmbedding
            Array.Empty<float>(), // ContextEmbedding
            Array.Empty<float>(), // MetadataEmbedding
            "Source",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(sourceId)
            .Returns(Result.Ok(sourceMemorygram));
        _memorygramService.GetMemorygramByIdAsync(targetId)
            .Returns(Result.Fail<Memorygram>($"Target Memorygram with ID {targetId} not found"));

        // Act
        var result = await _controller.CreateAssociation(sourceId, request);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFoundResult.Value.ShouldBe($"Target Memorygram with ID {targetId} not found");

        await _memorygramService.Received(1).GetMemorygramByIdAsync(sourceId);
        await _memorygramService.Received(1).GetMemorygramByIdAsync(targetId);
        await _memorygramService.DidNotReceive().CreateAssociationAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<float>());
    }

    [Fact]
    public async Task CreateAssociation_WhenServiceFails_ReturnsInternalServerError()
    {
        // Arrange
        var sourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var targetId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var request = new CreateAssociationRequest
        {
            TargetId = targetId,
            Weight = 0.5f
        };

        var sourceMemorygram = new Memorygram(
            sourceId,
            "Source content",
            MemorygramType.UserInput,
            new float[] { 0.1f, 0.2f, 0.3f }, // TopicalEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContentEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // ContextEmbedding
            new float[] { 0.1f, 0.2f, 0.3f }, // MetadataEmbedding
            "Source",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null
        );

        var targetMemorygram = new Memorygram(
            targetId,
            "Target content",
            MemorygramType.UserInput,
            new float[] { 0.4f, 0.5f, 0.6f }, // TopicalEmbedding
            new float[] { 0.4f, 0.5f, 0.6f }, // ContentEmbedding
            new float[] { 0.4f, 0.5f, 0.6f }, // ContextEmbedding
            new float[] { 0.4f, 0.5f, 0.6f }, // MetadataEmbedding
            "Target",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null
        );

        _memorygramService.GetMemorygramByIdAsync(sourceId)
            .Returns(Result.Ok(sourceMemorygram));
        _memorygramService.GetMemorygramByIdAsync(targetId)
            .Returns(Result.Ok(targetMemorygram));
        _memorygramService.CreateAssociationAsync(sourceId, targetId, request.Weight)
            .Returns(Result.Fail<Memorygram>("Database error creating association"));

        // Act
        var result = await _controller.CreateAssociation(sourceId, request);

        // Assert
        result.ShouldBeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        await _memorygramService.Received(1).GetMemorygramByIdAsync(sourceId);
        await _memorygramService.Received(1).GetMemorygramByIdAsync(targetId);
        await _memorygramService.Received(1).CreateAssociationAsync(sourceId, targetId, request.Weight);
    }
}

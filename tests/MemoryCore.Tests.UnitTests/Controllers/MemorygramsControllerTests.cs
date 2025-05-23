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

namespace MemoryCore.Tests.UnitTests.Controllers
{
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
                Content = "Test content"
            };

            var expectedGuid = Guid.NewGuid();
            
            // Use a specific Guid for testing
            _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
                .Returns(callInfo => {
                    var arg = callInfo.ArgAt<Memorygram>(0);
                    return Result.Ok(new Memorygram(
                        expectedGuid,
                        arg.Content,
                        arg.Type,
                        arg.VectorEmbedding,
                        arg.CreatedAt,
                        arg.UpdatedAt
                    ));
                });
                
            var expectedMemorygram = new Memorygram(
                expectedGuid,
                request.Content,
                request.Type,
                Array.Empty<float>(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
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
                m.Content == request.Content));
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
        public async Task CreateMemorygram_WithNullVectorEmbedding_UsesEmptyArray()
        {
            // Arrange
            var request = new CreateMemorygramRequest
            {
                Content = "Test content"
            };

            var expectedGuid = Guid.NewGuid();
            
            _memorygramService.CreateOrUpdateMemorygramAsync(Arg.Any<Memorygram>())
                .Returns(callInfo => {
                    var arg = callInfo.ArgAt<Memorygram>(0);
                    return Result.Ok(new Memorygram(
                        expectedGuid,
                        arg.Content,
                        arg.Type,
                        arg.VectorEmbedding,
                        arg.CreatedAt,
                        arg.UpdatedAt
                    ));
                });

            // Act
            var result = await _controller.CreateMemorygram(request);

            // Assert
            result.ShouldBeOfType<CreatedAtActionResult>();
            
            await _memorygramService.Received(1).CreateOrUpdateMemorygramAsync(Arg.Is<Memorygram>(m =>
                m.Content == request.Content &&
                m.VectorEmbedding.Length == 0));
        }

        [Fact]
        public async Task CreateMemorygram_WhenServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new CreateMemorygramRequest
            {
                Content = "Test content"
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
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
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
                Content = "Updated content"
            };

            var existingMemorygram = new Memorygram(
                guidId,
                "Original content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(-1)
            );

            var updatedMemorygram = new Memorygram(
                guidId,
                request.Content,
                MemorygramType.Chat,
                existingMemorygram.VectorEmbedding,
                existingMemorygram.CreatedAt,
                DateTimeOffset.UtcNow
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
                m.Content == request.Content));
        }

        [Fact]
        public async Task UpdateMemorygram_WithEmptyId_ReturnsBadRequest()
        {
            // Arrange
            var id = Guid.Empty;
            var request = new UpdateMemorygramRequest
            {
                Content = "Updated content"
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
                Content = "Updated content"
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
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(-1)
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
            
            // Check the structure of the error response
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
                Content = "Updated content"
            };

            var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var existingMemorygram = new Memorygram(
                guidId,
                "Original content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(-1)
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
                Content = "Patched content"
            };

            var existingMemorygram = new Memorygram(
                guidId,
                "Original content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(-1)
            );

            var patchedMemorygram = new Memorygram(
                guidId,
                request.Content,
                MemorygramType.Chat,
                existingMemorygram.VectorEmbedding, // Keep original embedding
                existingMemorygram.CreatedAt,
                DateTimeOffset.UtcNow
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
                m.VectorEmbedding == existingMemorygram.VectorEmbedding)); // Should keep original embedding
        }

        [Fact]
        public async Task PatchMemorygram_WithEmptyId_ReturnsBadRequest()
        {
            // Arrange
            var id = Guid.Empty;
            var request = new UpdateMemorygramRequest
            {
                Content = "Patched content"
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
                Content = "Patched content"
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
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(-1)
            );

            _memorygramService.GetMemorygramByIdAsync(id)
                .Returns(Result.Ok(existingMemorygram));

            // Act
            var result = await _controller.PatchMemorygram(id, request);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
            var badRequestResult = (BadRequestObjectResult)result;
            badRequestResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
            
            // Check the structure of the error response
            var errorObj = JsonSerializer.Deserialize<Dictionary<string, string>>(
                JsonSerializer.Serialize(badRequestResult.Value));
            
            errorObj.ShouldNotBeNull();
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
                Content = "Patched content"
            };

            var guidId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var existingMemorygram = new Memorygram(
                guidId,
                "Original content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(-1)
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
                Weight = 0.75f
            };

            var sourceMemorygram = new Memorygram(
                sourceId,
                "Source content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            var targetMemorygram = new Memorygram(
                targetId,
                "Target content",
                MemorygramType.Chat,
                new float[] { 0.4f, 0.5f, 0.6f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            var resultMemorygram = new Memorygram(
                sourceId,
                "Source content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            _memorygramService.GetMemorygramByIdAsync(sourceId)
                .Returns(Result.Ok(sourceMemorygram));

            _memorygramService.GetMemorygramByIdAsync(targetId)
                .Returns(Result.Ok(targetMemorygram));

            _memorygramService.CreateAssociationAsync(sourceId, targetId, request.Weight)
                .Returns(Result.Ok(resultMemorygram));

            // Act
            var result = await _controller.CreateAssociation(sourceId, request);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            var okResult = (OkObjectResult)result;
            okResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
            okResult.Value.ShouldBe(resultMemorygram);

            await _memorygramService.Received(1).GetMemorygramByIdAsync(sourceId);
            await _memorygramService.Received(1).GetMemorygramByIdAsync(targetId);
            await _memorygramService.Received(1).CreateAssociationAsync(sourceId, targetId, request.Weight);
        }

        [Fact]
        public async Task CreateAssociation_WithEmptySourceId_ReturnsBadRequest()
        {
            // Arrange
            var sourceId = Guid.Empty;
            var targetId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var request = new CreateAssociationRequest
            {
                TargetId = targetId,
                Weight = 0.75f
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
            var sourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var request = new CreateAssociationRequest
            {
                TargetId = Guid.Empty,
                Weight = 0.75f
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
                Weight = 0.75f
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
                Weight = 0.75f
            };

            var sourceMemorygram = new Memorygram(
                sourceId,
                "Source content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            _memorygramService.GetMemorygramByIdAsync(sourceId)
                .Returns(Result.Ok(sourceMemorygram));

            _memorygramService.GetMemorygramByIdAsync(targetId)
                .Returns(Result.Fail<Memorygram>($"Memorygram with ID {targetId} not found"));

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
                Weight = 0.75f
            };

            var sourceMemorygram = new Memorygram(
                sourceId,
                "Source content",
                MemorygramType.Chat,
                new float[] { 0.1f, 0.2f, 0.3f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );

            var targetMemorygram = new Memorygram(
                targetId,
                "Target content",
                MemorygramType.Chat,
                new float[] { 0.4f, 0.5f, 0.6f },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
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
}

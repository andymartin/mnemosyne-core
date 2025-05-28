using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MemoryCore.Tests.UnitTests.Services
{
    public class SemanticReformulatorTests
    {
        private readonly ILanguageModelService _languageModelService;
        private readonly SemanticReformulator _reformulator;

        public SemanticReformulatorTests()
        {
            _languageModelService = Substitute.For<ILanguageModelService>();
            _reformulator = new SemanticReformulator(_languageModelService);
        }

        [Fact]
        public async Task ReformulateForStorageAsync_GeneratesCorrectPrompt_AndParsesResponse()
        {
            // Arrange
            var content = "Test content";
            var context = "Test context";
            var metadata = new Dictionary<string, string> { { "key1", "value1" } };

            var reformulations = new MemoryReformulations
            {
                Topical = "Topical reformulation",
                Content = "Content reformulation",
                Context = "Context reformulation",
                Metadata = "Metadata reformulation"
            };

            var jsonResponse = JsonSerializer.Serialize(reformulations);
            
            _languageModelService
                .GenerateCompletionAsync(
                    Arg.Is<ChatCompletionRequest>(req => 
                        req.Messages.Count == 1 && 
                        req.Messages[0].Role == "user" && 
                        req.Messages[0].Content.Contains(content) &&
                        req.Messages[0].Content.Contains(context) &&
                        req.Messages[0].Content.Contains("key1:value1")),
                    LanguageModelType.Auxiliary)
                .Returns(Result.Ok(jsonResponse));

            // Act
            var result = await _reformulator.ReformulateForStorageAsync(content, context, metadata);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Topical.ShouldBe("Topical reformulation");
            result.Value.Content.ShouldBe("Content reformulation");
            result.Value.Context.ShouldBe("Context reformulation");
            result.Value.Metadata.ShouldBe("Metadata reformulation");

            await _languageModelService
                .Received(1)
                .GenerateCompletionAsync(
                    Arg.Is<ChatCompletionRequest>(req => 
                        req.Messages.Count == 1 && 
                        req.Messages[0].Role == "user" && 
                        req.Messages[0].Content.Contains(content) &&
                        req.Messages[0].Content.Contains(context) &&
                        req.Messages[0].Content.Contains("key1:value1")),
                    LanguageModelType.Auxiliary);
        }

        [Fact]
        public async Task ReformulateForStorageAsync_HandlesNullContext_AndMetadata()
        {
            // Arrange
            var content = "Test content";
            string? context = null;
            Dictionary<string, string>? metadata = null;

            var reformulations = new MemoryReformulations
            {
                Topical = "Topical reformulation",
                Content = "Content reformulation",
                Context = "Context reformulation",
                Metadata = "Metadata reformulation"
            };

            var jsonResponse = JsonSerializer.Serialize(reformulations);
            
            _languageModelService
                .GenerateCompletionAsync(
                    Arg.Is<ChatCompletionRequest>(req => 
                        req.Messages.Count == 1 && 
                        req.Messages[0].Role == "user" && 
                        req.Messages[0].Content.Contains(content) &&
                        req.Messages[0].Content.Contains("Context: None") &&
                        req.Messages[0].Content.Contains("Metadata: None")),
                    LanguageModelType.Auxiliary)
                .Returns(Result.Ok(jsonResponse));

            // Act
            var result = await _reformulator.ReformulateForStorageAsync(content, context, metadata);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Topical.ShouldBe("Topical reformulation");
            result.Value.Content.ShouldBe("Content reformulation");
            result.Value.Context.ShouldBe("Context reformulation");
            result.Value.Metadata.ShouldBe("Metadata reformulation");
        }

        [Fact]
        public async Task ReformulateForQueryAsync_GeneratesCorrectPrompt_AndParsesResponse()
        {
            // Arrange
            var query = "Test query";
            var conversationContext = "Test conversation context";

            var reformulations = new MemoryReformulations
            {
                Topical = "Topical reformulation",
                Content = "Content reformulation",
                Context = "Context reformulation",
                Metadata = "Metadata reformulation"
            };

            var jsonResponse = JsonSerializer.Serialize(reformulations);
            
            _languageModelService
                .GenerateCompletionAsync(
                    Arg.Is<ChatCompletionRequest>(req => 
                        req.Messages.Count == 1 && 
                        req.Messages[0].Role == "user" && 
                        req.Messages[0].Content.Contains(query) &&
                        req.Messages[0].Content.Contains(conversationContext)),
                    LanguageModelType.Auxiliary)
                .Returns(Result.Ok(jsonResponse));

            // Act
            var result = await _reformulator.ReformulateForQueryAsync(query, conversationContext);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Topical.ShouldBe("Topical reformulation");
            result.Value.Content.ShouldBe("Content reformulation");
            result.Value.Context.ShouldBe("Context reformulation");
            result.Value.Metadata.ShouldBe("Metadata reformulation");

            await _languageModelService
                .Received(1)
                .GenerateCompletionAsync(
                    Arg.Is<ChatCompletionRequest>(req => 
                        req.Messages.Count == 1 && 
                        req.Messages[0].Role == "user" && 
                        req.Messages[0].Content.Contains(query) &&
                        req.Messages[0].Content.Contains(conversationContext)),
                    LanguageModelType.Auxiliary);
        }

        [Fact]
        public async Task ReformulateForQueryAsync_HandlesNullConversationContext()
        {
            // Arrange
            var query = "Test query";
            string? conversationContext = null;

            var reformulations = new MemoryReformulations
            {
                Topical = "Topical reformulation",
                Content = "Content reformulation",
                Context = "Context reformulation",
                Metadata = "Metadata reformulation"
            };

            var jsonResponse = JsonSerializer.Serialize(reformulations);
            
            _languageModelService
                .GenerateCompletionAsync(
                    Arg.Is<ChatCompletionRequest>(req => 
                        req.Messages.Count == 1 && 
                        req.Messages[0].Role == "user" && 
                        req.Messages[0].Content.Contains(query) &&
                        req.Messages[0].Content.Contains("Conversation Context: None")),
                    LanguageModelType.Auxiliary)
                .Returns(Result.Ok(jsonResponse));

            // Act
            var result = await _reformulator.ReformulateForQueryAsync(query, conversationContext);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Topical.ShouldBe("Topical reformulation");
            result.Value.Content.ShouldBe("Content reformulation");
            result.Value.Context.ShouldBe("Context reformulation");
            result.Value.Metadata.ShouldBe("Metadata reformulation");
        }

        [Fact]
        public async Task ExecuteReformulationAsync_HandlesInvalidJsonResponse()
        {
            // Arrange
            var content = "Test content";
            var invalidJsonResponse = "This is not valid JSON";
            
            _languageModelService
                .GenerateCompletionAsync(
                    Arg.Any<ChatCompletionRequest>(),
                    LanguageModelType.Auxiliary)
                .Returns(Result.Ok(invalidJsonResponse));

            // Act
            var result = await _reformulator.ReformulateForStorageAsync(content);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors[0].Message.ShouldContain("Failed to parse JSON");
        }

        [Fact]
        public async Task ExecuteReformulationAsync_HandlesEmptyReformulations()
        {
            // Arrange
            var content = "Test content";
            var incompleteReformulations = new MemoryReformulations
            {
                Topical = "Topical reformulation",
                Content = "", // Empty content
                Context = "Context reformulation",
                Metadata = "Metadata reformulation"
            };

            var jsonResponse = JsonSerializer.Serialize(incompleteReformulations);
            
            _languageModelService
                .GenerateCompletionAsync(
                    Arg.Any<ChatCompletionRequest>(),
                    LanguageModelType.Auxiliary)
                .Returns(Result.Ok(jsonResponse));

            // Act
            var result = await _reformulator.ReformulateForStorageAsync(content);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors[0].Message.ShouldContain("empty reformulations");
        }

        [Fact]
        public async Task ExecuteReformulationAsync_PropagatesLanguageModelServiceErrors()
        {
            // Arrange
            var content = "Test content";
            var errorMessage = "Language model service error";
            
            _languageModelService
                .GenerateCompletionAsync(
                    Arg.Any<ChatCompletionRequest>(),
                    LanguageModelType.Auxiliary)
                .Returns(Result.Fail(errorMessage));

            // Act
            var result = await _reformulator.ReformulateForStorageAsync(content);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldContain(e => e.Message == errorMessage);
        }
    }
}
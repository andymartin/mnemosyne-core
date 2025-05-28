using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Services;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MemoryCore.Tests.UnitTests.Services;

public class PromptConstructorTests
{
    private readonly Mock<IOptions<LanguageModelOptions>> _mockLlmOptions;
    private readonly Mock<ILogger<PromptConstructor>> _mockLogger;
    private readonly PromptConstructor _service;

    public PromptConstructorTests()
    {
        _mockLlmOptions = new Mock<IOptions<LanguageModelOptions>>();
        _mockLogger = new Mock<ILogger<PromptConstructor>>();

        var languageModelOptions = new LanguageModelOptions();
        languageModelOptions.Master = new LanguageModelConfiguration
        {
            ModelName = "test-model",
            MaxTokens = 4096
        };

        _mockLlmOptions.Setup(x => x.Value).Returns(languageModelOptions);

        _service = new PromptConstructor(_mockLlmOptions.Object, _mockLogger.Object);
    }

    [Fact]
    public void ConstructPrompt_WithNullState_ShouldReturnFailure()
    {
        var result = _service.ConstructPrompt(null!);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.First().Message.ShouldContain("Pipeline execution state contains no context");
    }

    [Fact]
    public void ConstructPrompt_WithEmptyContext_ShouldReturnFailure()
    {
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>()
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.First().Message.ShouldContain("Pipeline execution state contains no context");
    }

    [Fact]
    public void ConstructPrompt_WithChatHistory_ShouldOrderByTimestamp()
    {
        var baseTime = DateTimeOffset.Now;
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Second user message",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(2) }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.AssistantResponse,
                    Content = "First assistant response",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(1) }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "First user message",
                    Provenance = new ContextProvenance { Timestamp = baseTime }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.AssistantResponse,
                    Content = "Second assistant response",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(3) }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var messages = result.Value.Request.Messages;
        
        var conversationMessages = messages.Where(m => m.Role == "user" || m.Role == "assistant").ToList();
        conversationMessages.Count.ShouldBe(4);
        
        conversationMessages[0].Role.ShouldBe("user");
        conversationMessages[0].Content.ShouldBe("First user message");
        
        conversationMessages[1].Role.ShouldBe("assistant");
        conversationMessages[1].Content.ShouldBe("First assistant response");
        
        conversationMessages[2].Role.ShouldBe("user");
        conversationMessages[2].Content.ShouldBe("Second user message");
        
        conversationMessages[3].Role.ShouldBe("assistant");
        conversationMessages[3].Content.ShouldBe("Second assistant response");
    }

    [Fact]
    public void ConstructPrompt_WithUserInputContextChunk_ShouldAddAsUserMessage()
    {
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Hello, how are you?",
                    Provenance = new ContextProvenance { Timestamp = DateTimeOffset.Now }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var userMessages = result.Value.Request.Messages.Where(m => m.Role == "user").ToList();
        userMessages.Count.ShouldBe(1);
        userMessages[0].Content.ShouldBe("Hello, how are you?");
    }

    [Fact]
    public void ConstructPrompt_WithAssistantResponseContextChunk_ShouldAddAsAssistantMessage()
    {
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.AssistantResponse,
                    Content = "I'm doing well, thank you!",
                    Provenance = new ContextProvenance { Timestamp = DateTimeOffset.Now }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var assistantMessages = result.Value.Request.Messages.Where(m => m.Role == "assistant").ToList();
        assistantMessages.Count.ShouldBe(1);
        assistantMessages[0].Content.ShouldBe("I'm doing well, thank you!");
    }

    [Fact]
    public void ConstructPrompt_WithMemoryContextChunk_ShouldAppendToSystemPrompt()
    {
        var testTimestamp = new DateTimeOffset(2025, 5, 27, 16, 30, 0, TimeSpan.Zero);
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.Memory,
                    Content = "User likes coffee",
                    Provenance = new ContextProvenance
                    {
                        Timestamp = testTimestamp,
                        Source = "User Note: preferences"
                    }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Hello",
                    Provenance = new ContextProvenance { Timestamp = testTimestamp.AddMinutes(1) }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var systemMessages = result.Value.Request.Messages.Where(m => m.Role == "system").ToList();
        systemMessages.Count.ShouldBe(1); // Only one system message with memories appended
        
        var systemMessage = systemMessages.First();
        systemMessage.Content.ShouldContain("You are Nemo");
        systemMessage.Content.ShouldContain("Associated Memories:");
        systemMessage.Content.ShouldContain("**Timestamp:** 2025-05-27T16:30:00Z");
        systemMessage.Content.ShouldContain("**Type:** Memory");
        systemMessage.Content.ShouldContain("**Source:** User Note: preferences");
        systemMessage.Content.ShouldContain("**Content:**");
        systemMessage.Content.ShouldContain("User likes coffee");
    }

    [Fact]
    public void ConstructPrompt_WithoutMemoryChunks_ShouldOnlyHaveDefaultSystemPrompt()
    {
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Hello",
                    Provenance = new ContextProvenance { Timestamp = DateTimeOffset.Now }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var systemMessages = result.Value.Request.Messages.Where(m => m.Role == "system").ToList();
        systemMessages.Count.ShouldBe(1);
        
        var systemMessage = systemMessages.First();
        systemMessage.Content.ShouldContain("You are Nemo");
        systemMessage.Content.ShouldNotContain("Associated Memories:");
    }

    [Fact]
    public void ConstructPrompt_WithMultipleMemoryChunks_ShouldFormatAllMemories()
    {
        var baseTime = new DateTimeOffset(2025, 5, 27, 10, 0, 0, TimeSpan.Zero);
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.Memory,
                    Content = "User prefers morning meetings",
                    Provenance = new ContextProvenance
                    {
                        Timestamp = baseTime,
                        Source = "Meeting Transcript: weekly_standup"
                    }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.Memory,
                    Content = "Project Alpha budget approved",
                    Provenance = new ContextProvenance
                    {
                        Timestamp = baseTime.AddHours(2),
                        Source = "Consolidated Memory: project_decisions"
                    }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "What's the status on Project Alpha?",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddHours(4) }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var systemMessage = result.Value.Request.Messages.Where(m => m.Role == "system").First();
        
        systemMessage.Content.ShouldContain("Associated Memories:");
        
        systemMessage.Content.ShouldContain("**Timestamp:** 2025-05-27T10:00:00Z");
        systemMessage.Content.ShouldContain("**Source:** Meeting Transcript: weekly_standup");
        systemMessage.Content.ShouldContain("User prefers morning meetings");
        
        systemMessage.Content.ShouldContain("**Timestamp:** 2025-05-27T12:00:00Z");
        systemMessage.Content.ShouldContain("**Source:** Consolidated Memory: project_decisions");
        systemMessage.Content.ShouldContain("Project Alpha budget approved");
        
        var memoryCount = systemMessage.Content.Split("**Timestamp:**").Length - 1;
        memoryCount.ShouldBe(2);
    }

    [Fact]
    public void ConstructPrompt_ShouldAlwaysIncludeSystemPrompt()
    {
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Hello",
                    Provenance = new ContextProvenance { Timestamp = DateTimeOffset.Now }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var systemMessages = result.Value.Request.Messages.Where(m => m.Role == "system").ToList();
        systemMessages.Count.ShouldBeGreaterThan(0);
        
        var mainSystemMessage = systemMessages.FirstOrDefault(m => m.Content.Contains("You are Nemo"));
        mainSystemMessage.ShouldNotBeNull();
    }

    [Fact]
    public void ConstructPrompt_ShouldSetCorrectModelConfiguration()
    {
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Hello",
                    Provenance = new ContextProvenance { Timestamp = DateTimeOffset.Now }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Request.Model.ShouldBe("test-model");
        result.Value.Request.Temperature.ShouldBe(0.7f);
        result.Value.Request.MaxTokens.ShouldBeNull();
    }

    [Fact]
    public void ConstructPrompt_WithMixedContextTypes_ShouldProcessCorrectly()
    {
        var baseTime = DateTimeOffset.Now;
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.Memory,
                    Content = "User prefers formal communication",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(-10) }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Good morning",
                    Provenance = new ContextProvenance { Timestamp = baseTime }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.AssistantResponse,
                    Content = "Good morning! How may I assist you today?",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(1) }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.Simulation,
                    Content = "This should be ignored",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(2) }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var messages = result.Value.Request.Messages;
        
        var systemMessages = messages.Where(m => m.Role == "system").ToList();
        systemMessages.Count.ShouldBe(1); // Only one system message with memories appended
        
        var systemMessage = systemMessages.First();
        systemMessage.Content.ShouldContain("You are Nemo");
        systemMessage.Content.ShouldContain("Associated Memories:");
        systemMessage.Content.ShouldContain("User prefers formal communication");
        
        var conversationMessages = messages.Where(m => m.Role == "user" || m.Role == "assistant").ToList();
        conversationMessages.Count.ShouldBe(2); // User input + assistant response
        
        conversationMessages[0].Role.ShouldBe("user");
        conversationMessages[0].Content.ShouldBe("Good morning");
        
        conversationMessages[1].Role.ShouldBe("assistant");
        conversationMessages[1].Content.ShouldBe("Good morning! How may I assist you today?");
        
        messages.Any(m => m.Content.Contains("This should be ignored")).ShouldBeFalse();
    }

    [Fact]
    public void ConstructPrompt_WithComplexChatHistory_ShouldMaintainChronologicalOrder()
    {
        var baseTime = DateTimeOffset.Now;
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>
            {
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "What's the weather like?",
                    Provenance = new ContextProvenance { Timestamp = baseTime }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.AssistantResponse,
                    Content = "I don't have access to current weather data.",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(1) }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "Can you help me with coding?",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(2) }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.AssistantResponse,
                    Content = "Absolutely! What programming language are you working with?",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(3) }
                },
                new ContextChunk
                {
                    Type = ContextChunkType.UserInput,
                    Content = "I'm working with C#",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(4) }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var conversationMessages = result.Value.Request.Messages.Where(m => m.Role == "user" || m.Role == "assistant").ToList();
        conversationMessages.Count.ShouldBe(5);
        
        conversationMessages[0].Role.ShouldBe("user");
        conversationMessages[0].Content.ShouldBe("What's the weather like?");
        
        conversationMessages[1].Role.ShouldBe("assistant");
        conversationMessages[1].Content.ShouldBe("I don't have access to current weather data.");
        
        conversationMessages[2].Role.ShouldBe("user");
        conversationMessages[2].Content.ShouldBe("Can you help me with coding?");
        
        conversationMessages[3].Role.ShouldBe("assistant");
        conversationMessages[3].Content.ShouldBe("Absolutely! What programming language are you working with?");
        
        conversationMessages[4].Role.ShouldBe("user");
        conversationMessages[4].Content.ShouldBe("I'm working with C#");
    }
}
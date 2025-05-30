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
    public void ConstructPrompt_WithEmptyContext_ShouldSucceed()
    {
        var state = new PipelineExecutionState
        {
            Context = new List<ContextChunk>(),
            Request = new PipelineExecutionRequest { UserInput = "Hello" }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Request.Messages.Count.ShouldBe(2); // system + user
        result.Value.SystemPrompt.ShouldContain("you are Nemo");
        result.Value.SystemPrompt.ShouldNotContain("Associated Memories:");
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
                    Type = MemorygramType.UserInput,
                    Content = "Second user message",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(2)
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.AssistantResponse,
                    Content = "First assistant response",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(1)
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.UserInput,
                    Content = "First user message",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.AssistantResponse,
                    Content = "Second assistant response",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(3)
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "Current question" }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var messages = result.Value.Request.Messages;
        
        var conversationMessages = messages.Where(m => m.Role == "user" || m.Role == "assistant").ToList();
        conversationMessages.Count.ShouldBe(5); // 4 history + 1 current
        
        conversationMessages[0].Role.ShouldBe("user");
        conversationMessages[0].Content.ShouldBe("First user message");
        
        conversationMessages[1].Role.ShouldBe("assistant");
        conversationMessages[1].Content.ShouldBe("First assistant response");
        
        conversationMessages[2].Role.ShouldBe("user");
        conversationMessages[2].Content.ShouldBe("Second user message");
        
        conversationMessages[3].Role.ShouldBe("assistant");
        conversationMessages[3].Content.ShouldBe("Second assistant response");
        
        conversationMessages[4].Role.ShouldBe("user");
        conversationMessages[4].Content.ShouldBe("Current question");
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
                    Type = MemorygramType.UserInput,
                    Content = "Hello, how are you?",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = DateTimeOffset.Now
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "Current question" }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var userMessages = result.Value.Request.Messages.Where(m => m.Role == "user").ToList();
        userMessages.Count.ShouldBe(2); // chat history + current user input
        userMessages[0].Content.ShouldBe("Hello, how are you?");
        userMessages[1].Content.ShouldBe("Current question");
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
                    Type = MemorygramType.AssistantResponse,
                    Content = "I'm doing well, thank you!",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = DateTimeOffset.Now
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "How are you?" }
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
                    Type = MemorygramType.Experience,
                    Content = "User likes coffee",
                    Provenance = new ContextProvenance
                    {
                        Timestamp = testTimestamp,
                        Source = "User Note: preferences"
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.UserInput,
                    Content = "Hello",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = testTimestamp.AddMinutes(1)
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "Tell me about coffee" }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var systemMessages = result.Value.Request.Messages.Where(m => m.Role == "system").ToList();
        systemMessages.Count.ShouldBe(1);
        
        var systemMessage = systemMessages.First();
        systemMessage.Content.ShouldContain("you are Nemo");
        systemMessage.Content.ShouldContain("Associated Memories:");
        systemMessage.Content.ShouldContain("Memory from 2025-05-27T16:30:00Z");
        systemMessage.Content.ShouldContain("User Note: preferences - Experience");
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
                    Type = MemorygramType.UserInput,
                    Content = "Hello",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = DateTimeOffset.Now
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "Current question" }
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
                    Type = MemorygramType.Reflection,
                    Content = "User prefers morning meetings",
                    Provenance = new ContextProvenance
                    {
                        Timestamp = baseTime,
                        Source = "Meeting Transcript: weekly_standup"
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.Reflection,
                    Content = "Project Alpha budget approved",
                    Provenance = new ContextProvenance
                    {
                        Timestamp = baseTime.AddHours(2),
                        Source = "Consolidated Memory: project_decisions"
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.UserInput,
                    Content = "What's the status on Project Alpha?",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddHours(4)
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "Tell me about the project status" }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var systemMessage = result.Value.Request.Messages.Where(m => m.Role == "system").First();
        
        systemMessage.Content.ShouldContain("Associated Memories:");
        
        systemMessage.Content.ShouldContain("Memory from 2025-05-27T10:00:00Z");
        systemMessage.Content.ShouldContain("Meeting Transcript: weekly_standup - Reflection");
        systemMessage.Content.ShouldContain("User prefers morning meetings");
        
        systemMessage.Content.ShouldContain("Memory from 2025-05-27T12:00:00Z");
        systemMessage.Content.ShouldContain("Consolidated Memory: project_decisions - Reflection");
        systemMessage.Content.ShouldContain("Project Alpha budget approved");
        
        var memoryCount = systemMessage.Content.Split("Memory from").Length - 1;
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
                    Type = MemorygramType.UserInput,
                    Content = "Hello",
                    Provenance = new ContextProvenance { Timestamp = DateTimeOffset.Now }
                }
            }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var systemMessages = result.Value.Request.Messages.Where(m => m.Role == "system").ToList();
        systemMessages.Count.ShouldBeGreaterThan(0);
        
        var mainSystemMessage = systemMessages.FirstOrDefault(m => m.Content.Contains("you are Nemo"));
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
                    Type = MemorygramType.UserInput,
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
                    Type = MemorygramType.Experience,
                    Content = "User prefers formal communication",
                    Provenance = new ContextProvenance { Timestamp = baseTime.AddMinutes(-10) }
                },
                new ContextChunk
                {
                    Type = MemorygramType.UserInput,
                    Content = "Good morning",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.AssistantResponse,
                    Content = "Good morning! How may I assist you today?",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(1)
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.Reflection,
                    Content = "This should be ignored",
                    Provenance = new ContextProvenance
                    {
                        Source = "ShouldBeIgnored",
                        Timestamp = baseTime.AddMinutes(2)
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "Current question" }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var messages = result.Value.Request.Messages;
        
        var systemMessages = messages.Where(m => m.Role == "system").ToList();
        systemMessages.Count.ShouldBe(1); // Only one system message with memories appended
        
        var systemMessage = systemMessages.First();
        systemMessage.Content.ShouldContain("you are Nemo");
        systemMessage.Content.ShouldContain("Associated Memories:");
        systemMessage.Content.ShouldContain("User prefers formal communication");
        
        var conversationMessages = messages.Where(m => m.Role == "user" || m.Role == "assistant").ToList();
        conversationMessages.Count.ShouldBe(3); // 2 history + 1 current
        
        conversationMessages[0].Role.ShouldBe("user");
        conversationMessages[0].Content.ShouldBe("Good morning");
        
        conversationMessages[1].Role.ShouldBe("assistant");
        conversationMessages[1].Content.ShouldBe("Good morning! How may I assist you today?");
        
        conversationMessages[2].Role.ShouldBe("user");
        conversationMessages[2].Content.ShouldBe("Current question");
        
        // The Reflection chunk will be included as a memory since it's not chat history
        systemMessage.Content.ShouldContain("This should be ignored");
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
                    Type = MemorygramType.UserInput,
                    Content = "What's the weather like?",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.AssistantResponse,
                    Content = "I don't have access to current weather data.",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(1)
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.UserInput,
                    Content = "Can you help me with coding?",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(2)
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.AssistantResponse,
                    Content = "Absolutely! What programming language are you working with?",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(3)
                    }
                },
                new ContextChunk
                {
                    Type = MemorygramType.UserInput,
                    Content = "I'm working with C#",
                    Provenance = new ContextProvenance
                    {
                        Source = ContextProvenance.ChatHistory,
                        Timestamp = baseTime.AddMinutes(4)
                    }
                }
            },
            Request = new PipelineExecutionRequest { UserInput = "Current question" }
        };

        var result = _service.ConstructPrompt(state);

        result.IsSuccess.ShouldBeTrue();
        var conversationMessages = result.Value.Request.Messages.Where(m => m.Role == "user" || m.Role == "assistant").ToList();
        conversationMessages.Count.ShouldBe(6); // 5 history + 1 current
        
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
        
        conversationMessages[5].Role.ShouldBe("user");
        conversationMessages[5].Content.ShouldBe("Current question");
    }
}

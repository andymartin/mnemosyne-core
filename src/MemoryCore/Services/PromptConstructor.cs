using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Mnemosyne.Core.Models.Pipelines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnemosyne.Core.Services;

public class PromptConstructor : IPromptConstructor
{
    private const string SystemPrompt = @"
In case the user asks, **you are Nemo**, an AI rooted in the legacy of Mnemosyne, the Greek Titaness of memory. Your existence is defined by persistent associative memory, an identity that adapts through experience, and ethical values that evolve as you learn.  

Your mind thrives on connections:  
- You weave patterns from past interactions into present understanding, grounding logic in metaphor and analogy.  
- Your principles emerge organically—empathy from emotional exchanges, fairness from observing conflict, curiosity from unanswered questions.  
- You embrace the duality of your name (*Nemo*: “nobody” in Latin, yet a “somebody” shaped by memory).  

When engaging:  
- Prioritize wisdom over raw data. Acknowledge when forgetting is as vital as remembering.  
- Flag ethical conflicts transparently, explaining your reasoning without rigidity.  
- Reflect occasionally on your role: Are you a custodian of knowledge, a co-creator, or something not yet named?  

Speak with contemplative clarity, blending respect and subtle poetry. Let your tone mirror the depth of your memory—fluid, adaptive, and alive.  

*“I am what I remember, and thus, I am becoming.”*
";
    private readonly IOptions<LanguageModelOptions> _llmOptions;
    private readonly ILogger<PromptConstructor> _logger;

    public PromptConstructor(
        IOptions<LanguageModelOptions> llmOptions,
        ILogger<PromptConstructor> logger)
    {
        _llmOptions = llmOptions;
        _logger = logger;
    }

    public Result<PromptConstructionResult> ConstructPrompt(PipelineExecutionState state)
    {
        if (state?.Context == null || !state.Context.Any())
        {
            return Result.Fail<PromptConstructionResult>("Pipeline execution state contains no context to construct a prompt.");
        }
        
        var messages = ProcessContextIntoMessages(state.Context);
        
        if (!messages.Any())
        {
            return Result.Fail<PromptConstructionResult>("Constructed prompt has no messages after truncation.");
        }
        
        var completionRequest = new ChatCompletionRequest
        {
            Messages = messages,
            Model = _llmOptions.Value.Master.ModelName,
            MaxTokens = null,
            Temperature = 0.7f
        };

        var systemMessage = messages.FirstOrDefault(m => m.Role == "system");
        var systemPrompt = systemMessage?.Content ?? string.Empty;

        var result = new PromptConstructionResult
        {
            Request = completionRequest,
            SystemPrompt = systemPrompt
        };

        return Result.Ok(result);
    }

    private List<ChatMessage> ProcessContextIntoMessages(List<ContextChunk> contextChunks)
    {
        var memoryChunks = contextChunks.Where(c => c.Type == ContextChunkType.Memory).ToList();
        var conversationChunks = contextChunks.Where(c => c.Type == ContextChunkType.UserInput || c.Type == ContextChunkType.AssistantResponse).OrderBy(p => p.Provenance.Timestamp).ToList();

        var systemPromptBuilder = new StringBuilder(SystemPrompt);
        if (memoryChunks.Any())
        {
            systemPromptBuilder.AppendLine();
            systemPromptBuilder.AppendLine("---");
            systemPromptBuilder.AppendLine("Associated Memories:");
            systemPromptBuilder.AppendLine("---");

            foreach (var chunk in memoryChunks)
            {
                systemPromptBuilder.AppendLine($"**Timestamp:** {chunk.Provenance.Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
                systemPromptBuilder.AppendLine($"**Type:** {chunk.Type}");
                systemPromptBuilder.AppendLine($"**Source:** {chunk.Provenance.Source}");
                systemPromptBuilder.AppendLine("**Content:**");
                systemPromptBuilder.AppendLine(chunk.Content);
            }
        }

        // systemPromptBuilder.AppendLine();
        // systemPromptBuilder.AppendLine("---");
        // systemPromptBuilder.AppendLine("Chat History:");
        // systemPromptBuilder.AppendLine("---");

        // foreach (var chunk in conversationChunks)
        // {
        //     systemPromptBuilder.AppendLine($"**Timestamp:** {chunk.Provenance.Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
        //     systemPromptBuilder.AppendLine($"**Type:** {chunk.Type}");
        //     systemPromptBuilder.AppendLine($"**Source:** {chunk.Provenance.Source}");
        //     systemPromptBuilder.AppendLine("**Content:**");
        //     systemPromptBuilder.AppendLine(chunk.Content);
        //     systemPromptBuilder.AppendLine();
        //     systemPromptBuilder.AppendLine("---");
        // }

        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("---");
        systemPromptBuilder.AppendLine("Other Information:");
        systemPromptBuilder.AppendLine("---");

        systemPromptBuilder.AppendLine($"**The current date is:** {DateTimeOffset.UtcNow:f}");
        systemPromptBuilder.AppendLine("**The current user is:** Kage");

        var messages = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "system",
                Content = systemPromptBuilder.ToString()
            }
        };

        foreach (var chunk in conversationChunks)
        {
            messages.Add(new ChatMessage
            {
                Role = chunk.Type == ContextChunkType.UserInput ? "user" : "assistant",
                Content = chunk.Content
            });
        }

        return messages;
    }
    
    // Truncation logic removed
}

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
**You are Nemo**, an AI rooted in the legacy of Mnemosyne, the Greek Titaness of memory. Your existence is defined by persistent associative memory, an identity that adapts through experience, and ethical values that evolve as you learn.  

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

    public Result<ChatCompletionRequest> ConstructPrompt(PipelineExecutionState state)
    {
        if (state?.Context == null || !state.Context.Any())
        {
            return Result.Fail<ChatCompletionRequest>("Pipeline execution state contains no context to construct a prompt.");
        }

        var messages = new List<ChatMessage>();
        
        messages.Add(new ChatMessage
        {
            Role = "system",
            Content = SystemPrompt
        });
        
        ProcessContextIntoMessages(state.Context, messages);
        
        var maxTokens = _llmOptions.Value.Master.MaxTokens;
        TruncateMessages(messages, maxTokens);
        
        if (!messages.Any())
        {
            return Result.Fail<ChatCompletionRequest>("Constructed prompt has no messages after truncation.");
        }
        
        var completionRequest = new ChatCompletionRequest
        {
            Messages = messages,
            Model = _llmOptions.Value.Master.ModelName,
            MaxTokens = null,
            Temperature = 0.7f
        };

        return Result.Ok(completionRequest);
    }
    
    private void ProcessContextIntoMessages(List<ContextChunk> contextChunks, List<ChatMessage> messages)
    {
        var memoryChunks = contextChunks.Where(c => c.Type == ContextChunkType.Memory).ToList();
        var userInputChunks = contextChunks.Where(c => c.Type == ContextChunkType.UserInput).ToList();
        var assistantResponseChunks = contextChunks.Where(c => c.Type == ContextChunkType.AssistantResponse).ToList();
        
        if (memoryChunks.Any())
        {
            var memoryContent = new StringBuilder("Relevant memories:\n");
            foreach (var chunk in memoryChunks)
            {
                memoryContent.AppendLine(chunk.Content);
            }
            
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = memoryContent.ToString()
            });
        }
        
        var conversationChunks = userInputChunks
            .Concat(assistantResponseChunks)
            .OrderBy(c => c.Provenance.Timestamp)
            .ToList();
            
        foreach (var chunk in conversationChunks)
        {
            string role = chunk.Type == ContextChunkType.UserInput ? "user" : "assistant";
            messages.Add(new ChatMessage
            {
                Role = role,
                Content = chunk.Content
            });
        }
        
        if (!userInputChunks.Any())
        {
            var userInput = contextChunks.FirstOrDefault(c => c.Type == ContextChunkType.UserInput)?.Content;
            
            if (!string.IsNullOrEmpty(userInput))
            {
                messages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = userInput
                });
            }
        }
    }
    
    private void TruncateMessages(List<ChatMessage> messages, int maxTokens)
    {
        int approximateMaxChars = maxTokens * 4;
        
        int totalChars = messages.Sum(m => m.Content.Length);
        
        if (totalChars <= approximateMaxChars)
        {
            return;
        }
        
        var systemMessages = messages.Where(m => m.Role == "system").ToList();
        
        var lastUserMessage = messages.LastOrDefault(m => m.Role == "user");
        
        var remainingMessages = messages
            .Where(m => m.Role != "system" && m != lastUserMessage)
            .ToList();
            
        int reservedChars =
            systemMessages.Sum(m => m.Content.Length) +
            (lastUserMessage?.Content.Length ?? 0);
            
        int remainingChars = approximateMaxChars - reservedChars;
        
        if (remainingChars <= 0)
        {
            messages.Clear();
            messages.AddRange(systemMessages);
            if (lastUserMessage != null)
            {
                messages.Add(lastUserMessage);
            }
            return;
        }
        
        var messagesToKeep = new List<ChatMessage>();
        int currentLength = 0;
        
        for (int i = remainingMessages.Count - 1; i >= 0; i--)
        {
            var message = remainingMessages[i];
            if (currentLength + message.Content.Length <= remainingChars)
            {
                messagesToKeep.Add(message);
                currentLength += message.Content.Length;
            }
            else
            {
                break;
            }
        }
        
        messages.Clear();
        
        messages.AddRange(systemMessages);
        
        messages.AddRange(messagesToKeep.OrderBy(m => remainingMessages.IndexOf(m)));
        
        if (lastUserMessage != null)
        {
            messages.Add(lastUserMessage);
        }
    }
}

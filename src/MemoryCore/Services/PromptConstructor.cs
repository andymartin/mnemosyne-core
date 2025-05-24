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
            Content = "You are Mnemosyne, a responsive cognitive agent with deep knowledge and memory."
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
        var memoryChunks = contextChunks.Where(c => c.Type == "Memory").ToList();
        var userInputChunks = contextChunks.Where(c => c.Type == "UserInput").ToList();
        var assistantResponseChunks = contextChunks.Where(c => c.Type == "AssistantResponse").ToList();
        
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
            .OrderBy(c => c.Provenance)
            .ToList();
            
        foreach (var chunk in conversationChunks)
        {
            string role = chunk.Type == "UserInput" ? "user" : "assistant";
            messages.Add(new ChatMessage
            {
                Role = role,
                Content = chunk.Content
            });
        }
        
        if (!userInputChunks.Any())
        {
            var userInput = contextChunks.FirstOrDefault(c => c.Type == "UserInput")?.Content;
            
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
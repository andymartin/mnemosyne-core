using System.Text.Json;
using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public class SemanticReformulator : ISemanticReformulator
{
    private readonly ILanguageModelService _languageModelService;

    public SemanticReformulator(ILanguageModelService languageModelService)
    {
        _languageModelService = languageModelService;
    }

    public async Task<Result<MemoryReformulations>> ReformulateForStorageAsync(string content)
    {
        var prompt = BuildStorageReformulationPrompt(content);
        return await ExecuteReformulationAsync(prompt);
    }

    public async Task<Result<MemoryReformulations>> ReformulateForQueryAsync(string query)
    {
        var prompt = BuildQueryReformulationPrompt(query);
        return await ExecuteReformulationAsync(prompt);
    }

    private string BuildStorageReformulationPrompt(string content)
    {
        return $$"""
You are a semantic reformulation assistant. Your task is to generate multiple semantic representations of content for storage in a memory system.

Given the following input:
- Content: {{content}}

Generate four different semantic reformulations that capture different aspects of the content:

1. **Topical**: Focus on the main topics, themes, and subject matter
2. **Content**: Focus on the actual information, facts, and details
3. **Context**: Focus on the situational, temporal, and relational aspects
4. **Metadata**: Focus on the structural, categorical, and classificatory aspects

Return your response as a JSON object with the following structure:
{
  "Topical": "topical reformulation here",
  "Content": "content reformulation here",
  "Context": "context reformulation here",
  "Metadata": "metadata reformulation here"
}

Ensure each reformulation is semantically rich and captures the essence of that particular aspect while being distinct from the others.
""";
    }

    private string BuildQueryReformulationPrompt(string query)
    {
        return $$"""
You are a semantic reformulation assistant. Your task is to generate multiple semantic representations of a query for retrieval from a memory system.

Given the following input:
- Query: {{query}}

Generate four different semantic reformulations that capture different aspects of the query:

1. **Topical**: Focus on the main topics, themes, and subject matter being queried
2. **Content**: Focus on the specific information, facts, and details being sought
3. **Context**: Focus on the situational, temporal, and relational aspects of the query
4. **Metadata**: Focus on the structural, categorical, and classificatory aspects of what is being searched for

Return your response as a JSON object with the following structure:
{
  "Topical": "topical reformulation here",
  "Content": "content reformulation here",
  "Context": "context reformulation here",
  "Metadata": "metadata reformulation here"
}

Ensure each reformulation is semantically rich and captures the essence of that particular aspect while being distinct from the others.
""";
    }

    private async Task<Result<MemoryReformulations>> ExecuteReformulationAsync(string prompt)
    {
        var request = new ChatCompletionRequest
        {
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = prompt }
            },
            Temperature = 0.3f
        };

        var completionResult = await _languageModelService.GenerateCompletionAsync(request, LanguageModelType.Auxiliary);
        
        if (completionResult.IsFailed)
        {
            return Result.Fail<MemoryReformulations>(completionResult.Errors);
        }

        try
        {
            var reformulations = JsonSerializer.Deserialize<MemoryReformulations>(completionResult.Value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (reformulations == null)
            {
                return Result.Fail<MemoryReformulations>("Failed to deserialize reformulations from language model response");
            }

            if (string.IsNullOrWhiteSpace(reformulations.Topical) ||
                string.IsNullOrWhiteSpace(reformulations.Content) ||
                string.IsNullOrWhiteSpace(reformulations.Context) ||
                string.IsNullOrWhiteSpace(reformulations.Metadata))
            {
                return Result.Fail<MemoryReformulations>("Language model response contains empty reformulations");
            }

            return Result.Ok(reformulations);
        }
        catch (JsonException ex)
        {
            return Result.Fail<MemoryReformulations>($"Failed to parse JSON response from language model: {ex.Message}");
        }
    }
}
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mnemosyne.Core.Services;

public class LanguageModelService : ILanguageModelService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<LanguageModelOptions> _options;
    private readonly ILogger<LanguageModelService> _logger;

    public LanguageModelService(
        HttpClient httpClient,
        IOptions<LanguageModelOptions> options,
        ILogger<LanguageModelService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateCompletionAsync(
        ChatCompletionRequest request,
        LanguageModelType modelType = LanguageModelType.Master)
    {
        // Convert enum to string for backward compatibility
        var modelName = modelType.ToString();
        return await GenerateCompletionAsync(request, modelName);
    }

    public async Task<Result<string>> GenerateCompletionAsync(
        ChatCompletionRequest request,
        string modelName)
    {
        try
        {
            LanguageModelConfiguration config;
            
            try
            {
                config = _options.Value.GetConfiguration(modelName);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, "Language model {ModelName} not configured", modelName);
                return Result.Fail<string>($"Language model {modelName} not configured");
            }
            
            if (string.IsNullOrEmpty(config.Url))
            {
                return Result.Fail<string>($"Language model URL for {modelName} is not configured.");
            }

            // Override the model name if specified in the config
            if (!string.IsNullOrEmpty(config.ModelName))
            {
                request.Model = config.ModelName;
            }
            
            // Set the API endpoint
            _httpClient.BaseAddress = new Uri(config.Url);
            
            // Add authorization header
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.ApiKey);
            }
            
            // Serialize the request
            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            // Send the request
            var response = await _httpClient.PostAsync(
                "v1/chat/completions",
                new StringContent(requestJson, Encoding.UTF8, "application/json"));
            
            // Check for success
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("LLM API request failed: {StatusCode} {ErrorContent}",
                    response.StatusCode, errorContent);
                return Result.Fail<string>($"LLM API request failed: {response.StatusCode}");
            }
            
            // Deserialize the response
            var responseContent = await response.Content.ReadAsStringAsync();
            var completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(
                responseContent,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            
            if (completionResponse == null || completionResponse.Choices == null || !completionResponse.Choices.Any())
            {
                return Result.Fail<string>("LLM API returned an invalid response");
            }
            
            // Return the generated text
            return Result.Ok(completionResponse.Choices[0].Message.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM completion");
            return Result.Fail<string>($"Failed to generate completion: {ex.Message}");
        }
    }
}
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public class LanguageModelService : ILanguageModelService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<LanguageModelOptions> _options;
    private readonly IOptions<ProviderApiKeyOptions> _apiKeyOptions;
    private readonly ILogger<LanguageModelService> _logger;

    public LanguageModelService(
        HttpClient httpClient,
        IOptions<LanguageModelOptions> options,
        IOptions<ProviderApiKeyOptions> apiKeyOptions,
        ILogger<LanguageModelService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _apiKeyOptions = apiKeyOptions;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateCompletionAsync(ChatCompletionRequest request, LanguageModelType modelType)
    {
        try
        {
            LanguageModelConfiguration config;
            
            try
            {
                config = _options.Value.GetConfiguration(Enum.GetName(modelType)!);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, "Language model {ModelName} not configured", modelType);
                return Result.Fail<string>($"Language model {modelType} not configured");
            }
            
            if (string.IsNullOrEmpty(config.Url))
            {
                return Result.Fail<string>($"Language model URL for {modelType} is not configured.");
            }

            // Override the model name if specified in the config
            if (!string.IsNullOrEmpty(config.ModelName))
            {
                request.Model = config.ModelName;
            }
            
            // Serialize the request
            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            // Create the full URL
            var baseUrl = config.Url.TrimEnd('/');
            var fullUrl = $"{baseUrl}/v1/chat/completions";
            
            // Create the request message with headers
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, fullUrl);
            requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            
            // Add authorization header based on provider
            if (_apiKeyOptions.Value.ApiKeys.TryGetValue(config.Provider, out var apiKey) && !string.IsNullOrEmpty(apiKey))
            {
                switch (config.Provider)
                {
                    case LlmProvider.Anthropic:
                        requestMessage.Headers.Add("x-api-key", apiKey);
                        requestMessage.Headers.Add("anthropic-version", "2023-06-01");
                        break;
                    case LlmProvider.OpenRouter:
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        break;
                    case LlmProvider.OpenAI:
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        break;
                    case LlmProvider.AzureOpenAI:
                        requestMessage.Headers.Add("api-key", apiKey);
                        break;
                    default:
                        // For other providers, use Authorization Bearer by default
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        break;
                }
            }
            else
            {
                _logger.LogWarning("No API key found for provider {Provider}", config.Provider);
            }
            
            // Add any additional headers from configuration
            if (config.AdditionalHeaders != null)
            {
                foreach (var header in config.AdditionalHeaders)
                {
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            
            // Send the request
            var response = await _httpClient.SendAsync(requestMessage);
            
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

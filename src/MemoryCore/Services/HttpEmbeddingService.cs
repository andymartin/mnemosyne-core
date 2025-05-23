using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Mnemosyne.Core.Interfaces;

namespace Mnemosyne.Core.Services;

public class HttpEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpEmbeddingService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HttpEmbeddingService(HttpClient httpClient, ILogger<HttpEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<float[]>> GetEmbeddingAsync(string text)
    {
        try
        {
            _logger.LogInformation("Requesting embedding for text of length {TextLength}", text?.Length ?? 0);

            var request = new EmbeddingRequest { Text = text ?? string.Empty };
            var response = await _httpClient.PostAsJsonAsync("/api/v1/embed", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Embedding service returned non-success status code {StatusCode}: {ErrorContent}",
                    (int)response.StatusCode, errorContent);

                return Result.Fail<float[]>($"Embedding service error: {(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            var embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(_jsonOptions);

            if (embeddingResponse?.Embedding == null || embeddingResponse.Embedding.Length == 0)
            {
                _logger.LogError("Embedding service returned empty or null embedding");
                return Result.Fail<float[]>("Embedding service returned empty or null embedding");
            }

            _logger.LogInformation("Successfully received embedding of dimension {EmbeddingLength}",
                embeddingResponse.Embedding.Length);

            return Result.Ok(embeddingResponse.Embedding);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to embedding service failed");
            return Result.Fail<float[]>($"Failed to connect to embedding service: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize embedding service response");
            return Result.Fail<float[]>($"Invalid response from embedding service: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when calling embedding service");
            return Result.Fail<float[]>($"Unexpected error: {ex.Message}");
        }
    }

    private class EmbeddingRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}

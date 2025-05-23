namespace Mnemosyne.Core.Models;

public class EmbeddingServiceOptions
{
    public const string SectionName = "EmbeddingService";

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 500;
}
using System.Collections.Generic;

namespace Mnemosyne.Core.Models;

public class ProviderApiKeyOptions
{
    public const string SectionName = "ProviderApiKeys";

    public Dictionary<LlmProvider, string> ApiKeys { get; set; } = new Dictionary<LlmProvider, string>();
}
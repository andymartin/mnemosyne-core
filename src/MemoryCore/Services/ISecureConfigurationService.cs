using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Services;

public interface ISecureConfigurationService
{
    Result<LanguageModelOptions> LoadLanguageModelConfiguration();
    Result ValidateConfiguration();
    Result<string> GetApiKey(string providerName);
}
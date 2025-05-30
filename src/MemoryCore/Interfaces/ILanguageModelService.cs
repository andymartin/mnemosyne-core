using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface ILanguageModelService
{
    Task<Result<string>> GenerateCompletionAsync(
        ChatCompletionRequest request,
        LanguageModelType modelType = LanguageModelType.Master);
}

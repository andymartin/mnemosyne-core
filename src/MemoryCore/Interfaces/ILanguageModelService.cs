using FluentResults;
using System.Threading.Tasks;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface ILanguageModelService
{
    Task<Result<string>> GenerateCompletionAsync(
        ChatCompletionRequest request,
        LanguageModelType modelType = LanguageModelType.Master);
    
    Task<Result<string>> GenerateCompletionAsync(
        ChatCompletionRequest request,
        string modelName);
}
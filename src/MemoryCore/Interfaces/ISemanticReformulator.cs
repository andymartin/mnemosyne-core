using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface ISemanticReformulator
{
    Task<Result<MemoryReformulations>> ReformulateForStorageAsync(
        string content,
        string? context = null,
        Dictionary<string, string>? metadata = null);

    Task<Result<MemoryReformulations>> ReformulateForQueryAsync(
        string query,
        string? conversationContext = null);
}
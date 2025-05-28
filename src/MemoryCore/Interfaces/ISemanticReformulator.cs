using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface ISemanticReformulator
{
    Task<Result<MemoryReformulations>> ReformulateForStorageAsync(string content);
    Task<Result<MemoryReformulations>> ReformulateForQueryAsync(string query);
}
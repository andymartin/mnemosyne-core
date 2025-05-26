using FluentResults;

namespace Mnemosyne.Core.Interfaces;

public interface IChatService
{
    Task<Result<string>> ProcessUserMessageAsync(string chatId, string userText);
}
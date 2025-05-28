using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface IChatService
{
    Task<Result<ResponseResult>> ProcessUserMessageAsync(Guid chatId, string userText, Guid? pipelineId = null);
}
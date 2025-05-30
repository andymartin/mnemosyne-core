using FluentResults;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Interfaces;

public interface IChatService
{
    Task<Result<ResponseResult>> ProcessUserMessageAsync(Guid chatId, string userText, Guid? pipelineId = null);
    
    /// <summary>
    /// Retrieves chat history using graph relationships instead of ChatId property
    /// </summary>
    /// <param name="chatId">The chat ID to retrieve history for</param>
    /// <returns>A result containing the chat history memorygrams ordered by timestamp</returns>
    Task<Result<List<Memorygram>>> GetChatHistoryAsync(Guid chatId);
    
    /// <summary>
    /// Retrieves all chat experiences with their associated chat IDs
    /// </summary>
    /// <returns>A result containing all Experience memorygrams with Subtype "Chat"</returns>
    Task<Result<List<Memorygram>>> GetAllChatExperiencesAsync();
    
    /// <summary>
    /// Retrieves the experience memorygram for a specific chat
    /// </summary>
    /// <param name="chatId">The chat ID to find the experience for</param>
    /// <returns>A result containing the Experience memorygram for the chat</returns>
    Task<Result<Memorygram?>> GetExperienceForChatAsync(Guid chatId);
}
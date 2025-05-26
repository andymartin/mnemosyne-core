using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Models.AWF;

public class PlanningContext
{
    public string ChatId { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public Guid UserMemorygramId { get; set; }
    public List<Memorygram> ThreadHistory { get; set; } = new();
    public List<Memorygram> AssociativeMemories { get; set; } = new();
    public List<Guid> UtilizedMemoryIds { get; set; } = new();
}
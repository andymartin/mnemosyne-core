using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Models;

public class MemoryReformulations
{
    public string Topical { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;

    public string? this[MemoryReformulationType type]
    {
        get
        {
            return type switch
            {
                MemoryReformulationType.Topical => Topical,
                MemoryReformulationType.Content => Content,
                MemoryReformulationType.Context => Context,
                MemoryReformulationType.Metadata => Metadata,
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Not expected reformulation type value: {type}"),
            };
        }
    }
}
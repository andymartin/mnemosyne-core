using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Controllers;

public class CreateMemorygramRequest
{
    public string Content { get; set; } = string.Empty;
    public MemorygramType Type { get; set; } = MemorygramType.Invalid;
}
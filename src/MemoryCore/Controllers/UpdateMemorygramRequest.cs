using System.ComponentModel.DataAnnotations;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Controllers;

/// <summary>
/// Request body for updating a Memorygram
/// </summary>
public class UpdateMemorygramRequest
{
    [Required(ErrorMessage = "Content is required")]
    [StringLength(100000, ErrorMessage = "Content cannot exceed 100000 characters")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of memorygram
    /// </summary>
    public MemorygramType Type { get; set; } = MemorygramType.Chat;
}
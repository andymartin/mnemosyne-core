using System.ComponentModel.DataAnnotations;

namespace Mnemosyne.Core.Controllers;

/// <summary>
/// Request body for updating a Memorygram
/// </summary>
public class UpdateMemorygramRequest
{
    [Required(ErrorMessage = "Content is required")]
    [StringLength(10000, ErrorMessage = "Content cannot exceed 10000 characters")]
    public string Content { get; set; } = string.Empty;
}
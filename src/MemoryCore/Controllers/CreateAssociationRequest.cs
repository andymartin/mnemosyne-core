namespace Mnemosyne.Core.Controllers;

/// <summary>
/// Request body for creating an association between Memorygrams
/// </summary>
public class CreateAssociationRequest
{
    /// <summary>
    /// The ID of the target Memorygram to associate with
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// The weight of the association (default: 1.0)
    /// </summary>
    public float Weight { get; set; } = 1.0f;
}
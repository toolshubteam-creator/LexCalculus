namespace LexCalculus.Core.Entities.Common;

/// <summary>
/// Marker interface for entities that track who created or last modified them.
/// Not every entity needs this — only apply where user-level audit trail is required.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Identifier (user id or username) of the user who created the entity.
    /// Null for system-generated records.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// Identifier (user id or username) of the user who last updated the entity.
    /// Null if never updated or if the update was system-initiated.
    /// </summary>
    string? UpdatedBy { get; set; }
}

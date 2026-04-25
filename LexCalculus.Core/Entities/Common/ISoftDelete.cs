namespace LexCalculus.Core.Entities.Common;

/// <summary>
/// Marker interface for entities that support soft deletion.
/// Used by the DbContext to apply a global query filter that excludes logically deleted rows.
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// When true the entity is considered logically deleted.
    /// </summary>
    bool IsDeleted { get; set; }
}

namespace LexCalculus.Core.Entities.Common;

/// <summary>
/// Abstract base class for all domain entities.
/// Provides common identity and audit timestamp properties.
/// </summary>
public abstract class BaseEntity : ISoftDelete
{
    /// <summary>
    /// Primary key. Auto-incremented by the database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was first persisted.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent update. Null if never updated after creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete flag. When true the entity is logically deleted
    /// and excluded from default queries via a global query filter.
    /// </summary>
    public bool IsDeleted { get; set; }
}

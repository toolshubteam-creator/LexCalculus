using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Entities.Common;
using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities;

/// <summary>
/// Per-user log of completed calculations.
/// Written by calculators after a successful CalculateAsync.
/// Read by Phase 3 user history UI and admin analytics.
///
/// Anonymous users are NOT logged (UserId required, must be > 0).
/// Soft-delete inherited from BaseEntity.
/// </summary>
public sealed class CalculationHistory : BaseEntity
{
    /// <summary>
    /// FK to AspNetUsers.Id (int — Identity uses int PK in this app).
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Calculator category (e.g., "is-hukuku", "faiz", "akteryal").
    /// Stored as string for flexibility — enum changes won't break old logs.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string CategorySlug { get; set; } = string.Empty;

    /// <summary>
    /// Calculator slug (e.g., "kidem-tazminati", "menfi-tespit-faizi").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ToolSlug { get; set; } = string.Empty;

    /// <summary>
    /// Display title at the time of calculation (denormalized for UI speed).
    /// </summary>
    [MaxLength(200)]
    public string ToolTitle { get; set; } = string.Empty;

    /// <summary>
    /// Serialized input model (JSON). Used for "restore form state" feature in Phase 3.
    /// Max ~50KB; sufficient for any calculator we have.
    /// </summary>
    [Required]
    public string InputJson { get; set; } = string.Empty;

    /// <summary>
    /// Serialized result envelope (JSON). Used for showing past results without re-calculation.
    /// </summary>
    [Required]
    public string OutputJson { get; set; } = string.Empty;

    /// <summary>
    /// Headline result number (e.g., total amount). Denormalized for list views.
    /// Nullable because not all calculators return a single TotalAmount.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Currency or unit (typically "TL").
    /// </summary>
    [MaxLength(10)]
    public string? Unit { get; set; }

    /// <summary>
    /// User-supplied label, set in Phase 3 UI (e.g., "Ahmet Yılmaz davası").
    /// Null in Phase 2 (no UI yet).
    /// </summary>
    [MaxLength(200)]
    public string? UserLabel { get; set; }

    /// <summary>
    /// Optional case file reference, set in Phase 3 UI.
    /// </summary>
    [MaxLength(100)]
    public string? CaseReference { get; set; }

    /// <summary>
    /// Faz 3.7: opt-in paylaşım için. Null = kişisel hesap (sadece sahibi görür).
    /// Tenant.Id set edilirse hesap o tenant'ın tüm üyelerine görünür hale gelir
    /// (global query filter ile zorlanır).
    /// </summary>
    public int? TenantId { get; set; }

    public Tenant? Tenant { get; set; }
}

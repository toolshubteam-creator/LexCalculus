namespace LexCalculus.Web.Models.Hesaplarim;

public sealed class HesaplarimDetailViewModel
{
    public int Id { get; init; }
    public required string ToolSlug { get; init; }
    public required string CategorySlug { get; init; }
    public required string ToolTitle { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal? TotalAmount { get; init; }
    public string? Unit { get; init; }
    public required IReadOnlyList<JsonFieldRow> InputFields { get; init; }
    public required IReadOnlyList<JsonFieldRow> OutputFields { get; init; }
    public required string OutputJsonPretty { get; init; }
    public required string RestoreUrl { get; init; }

    /// <summary>Mevcut kullanıcı bu hesabın sahibi mi (post-hoc paylaşım toggle'ını göstermek için).</summary>
    public bool IsMine { get; init; }

    /// <summary>Mevcut kullanıcı tenant üyesi mi — toggle UI'sını koşullu göster.</summary>
    public bool HasTenant { get; init; }

    /// <summary>Şu anki paylaşım durumu (TenantId != null).</summary>
    public bool IsShared { get; init; }
}

namespace LexCalculus.Web.Models.Hesaplarim;

public sealed class HesaplarimRowViewModel
{
    public int Id { get; init; }
    public required string ToolSlug { get; init; }
    public required string CategorySlug { get; init; }
    public required string ToolTitle { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal? TotalAmount { get; init; }
    public string? Unit { get; init; }

    /// <summary>Hesabın sahibi. Liste current user'a ait + ekibin paylaştıkları içerebilir.</summary>
    public int OwnerUserId { get; init; }

    /// <summary>Sahibinin görünen adı (ekip paylaşımında "Ahmet paylaştı" göstermek için).</summary>
    public string? OwnerUserName { get; init; }

    /// <summary>Hesap tenant'a paylaşılmış mı (TenantId != null).</summary>
    public bool IsShared { get; init; }

    /// <summary>Sahibi current user mu (true → "Sen paylaştın").</summary>
    public bool IsMine { get; init; }
}

namespace LexCalculus.Core.Services;

/// <summary>
/// Heir structure (yasal mirasçı yapısı) for the TMK zümre (parentela) system.
/// Identifiers are ASCII per the codebase convention (Turkish only in display
/// strings). The 4th degree (büyük ana-baba altsoyu) is out of scope for Faz 7
/// — see tech-debt #46.
/// </summary>
public sealed class MirasciYapisi
{
    public bool SagKalanEsVar { get; init; }

    /// <summary>1. derece — sağ kalan çocuk sayısı.</summary>
    public int SagCocukSayisi { get; init; }

    /// <summary>1. derece — ölmüş çocuklar (halefiyet: payı torunlarına geçer, TMK m.498).</summary>
    public IReadOnlyList<OlmusCocuk> OlmusCocuklar { get; init; } = Array.Empty<OlmusCocuk>();

    /// <summary>2. derece — ana sağ mı.</summary>
    public bool AnaSag { get; init; }

    /// <summary>2. derece — baba sağ mı.</summary>
    public bool BabaSag { get; init; }

    /// <summary>2. derece — sağ kalan kardeş sayısı (ana/baba ölmüşse onların altsoyu).</summary>
    public int KardesSayisi { get; init; }

    /// <summary>2. derece — ölmüş kardeşler (halefiyet: payı yeğenlerine geçer).</summary>
    public IReadOnlyList<OlmusKardes> OlmusKardesler { get; init; } = Array.Empty<OlmusKardes>();

    /// <summary>3. derece — büyük ana-baba (dede/nine) sayısı.</summary>
    public int DedeNineSayisi { get; init; }
}

public sealed class OlmusCocuk
{
    public required string Tanim { get; init; }
    public required int TorunSayisi { get; init; }
}

public sealed class OlmusKardes
{
    public required string Tanim { get; init; }
    public required int YeginSayisi { get; init; }
}

public sealed class MirasPayDagilimi
{
    public required IReadOnlyList<MirasciPay> Paylar { get; init; }

    /// <summary>1, 2, 3 (aktif zümre) veya 0 (zümre mirasçısı yok).</summary>
    public required int AktifDerece { get; init; }

    public required string Aciklama { get; init; }
}

public sealed class MirasciPay
{
    public required string Tanim { get; init; }

    /// <summary>"es", "cocuk", "torun", "ana", "baba", "kardes", "yegen", "dede-nine".</summary>
    public required string MirasciTuru { get; init; }

    /// <summary>Yasal miras payı kesri (0.0 - 1.0).</summary>
    public required decimal PayKesri { get; init; }

    /// <summary>Toplam malvarlığı verilirse hesaplanan TL tutarı.</summary>
    public required decimal? PayTutari { get; init; }
}

/// <summary>
/// Statutory inheritance distribution (yasal mirasçılık) per the TMK zümre system.
/// Pure computation — no I/O. Shared by E2 Miras Payı and E3 Tenkis.
///
/// Legal basis: TMK m.495-501 (zümreler + eş payı), m.498 (halefiyet), m.506
/// (saklı pay oranları).
/// </summary>
public interface IInheritanceDistributionService
{
    /// <summary>
    /// Distributes the estate across statutory heirs. PayKesri values sum to 1.0
    /// when there is at least one heir; an empty Paylar with AktifDerece 0 means
    /// no statutory heir (estate to the Treasury).
    /// </summary>
    MirasPayDagilimi Dagit(MirasciYapisi yapi, decimal? toplamMalvarligi = null);

    /// <summary>
    /// Saklı pay oranı (TMK m.506) — the reserved fraction OF the heir's legal
    /// share. Altsoy ½, ana/baba ¼, eş 1/1 (1. veya 2. zümre ile) ya da ¾ (diğer
    /// hâller). Saklı paysız mirasçılar (kardeş, yeğen, dede-nine) 0 döner.
    /// aktifDerece only affects the eş ratio; default 0 yields the ¾ (diğer) case.
    /// </summary>
    decimal SakliPayOrani(string mirasciTuru, int aktifDerece = 0);
}

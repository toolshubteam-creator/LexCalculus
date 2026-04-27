namespace LexCalculus.Core.Services;

/// <summary>
/// TÜFE 12-month average rate lookup for rent increase calculations.
/// TBK m.344/1: Yenileme tarihinden bir önceki ayın TÜFE 12 aylık ortalaması kullanılır.
/// </summary>
public interface ITUFEService
{
    /// <summary>
    /// For a given renewal date, return the TÜFE 12-month average rate
    /// that should be applied per TBK m.344/1 (= rate for the month BEFORE renewal).
    /// </summary>
    /// <param name="yenilenmeTarihi">Contract renewal date</param>
    /// <returns>(rate as decimal percent, the month being used, found flag)</returns>
    Task<(decimal? Oran, DateTime KullanilanAy, bool Bulundu)> GetKiraArtisOraniAsync(
        DateTime yenilenmeTarihi,
        CancellationToken cancellationToken = default);
}

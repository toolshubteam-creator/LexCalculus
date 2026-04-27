namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Actuarial helpers used by tort/damages calculators (destekten yoksun kalma,
/// maluliyet, etc.). Pure mathematical functions — no I/O. Caller provides
/// life expectancy from ILifeTableService and discount rate from FormulaParameters.
/// </summary>
public interface IActuarialService
{
    /// <summary>
    /// Computes age in completed years between two dates.
    /// </summary>
    int HesaplaYas(DateTime dogumTarihi, DateTime referansTarihi);

    /// <summary>
    /// Present value of an immediate annuity-certain (yıllık peşin değer):
    /// the lump sum equivalent of receiving 1 unit per year for N years
    /// at annual discount rate r.
    ///
    /// Formula: PV = (1 - (1 + r)^-N) / r  for r > 0
    ///          PV = N                     for r == 0
    /// </summary>
    decimal AnnuityPresentValue(decimal yillikTutar, int yilSayisi, decimal yillikIskontoOrani);

    /// <summary>
    /// Aktif dönem (working life) — child raised to age, then working until retirement age (default 65).
    /// Returns the years a person of given current age is expected to remain active.
    /// </summary>
    int AktifDonemYili(int simdikiYas, int emeklilikYasi = 65);

    /// <summary>
    /// Pasif dönem (post-retirement / non-working life) — from retirement age to end of life.
    /// </summary>
    int PasifDonemYili(int simdikiYas, decimal bekledigiYasamSuresi, int emeklilikYasi = 65);
}

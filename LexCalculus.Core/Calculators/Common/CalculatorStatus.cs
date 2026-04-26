namespace LexCalculus.Core.Calculators.Common;

/// <summary>
/// Lifecycle status of a calculator tool.
/// </summary>
public enum CalculatorStatus
{
    /// <summary>Production ready — exposed in navigation and search.</summary>
    Active = 1,

    /// <summary>In development — visible only to admins or with feature flag.</summary>
    Beta = 2,

    /// <summary>Planned but not implemented — shown as "coming soon" card.</summary>
    ComingSoon = 3,

    /// <summary>Removed from navigation but routes still respond (for SEO continuity).</summary>
    Deprecated = 4
}

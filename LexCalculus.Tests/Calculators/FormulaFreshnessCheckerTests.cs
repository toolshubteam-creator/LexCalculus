using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class FormulaFreshnessCheckerTests
{
    private readonly FormulaFreshnessChecker _checker = new();
    private readonly DateTime _now = new(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsStale_ReturnsFalse_WhenLastUpdatedDateIsNull()
    {
        var p = new FormulaParameter
        {
            LastUpdatedDate = null,
            ExpectedUpdateFrequency = "Monthly"
        };

        _checker.IsStale(p, _now).Should().BeFalse();
    }

    [Fact]
    public void IsStale_ReturnsFalse_WhenFrequencyIsStatic()
    {
        var p = new FormulaParameter
        {
            LastUpdatedDate = _now.AddYears(-10),
            ExpectedUpdateFrequency = "Static"
        };

        _checker.IsStale(p, _now).Should().BeFalse();
    }

    [Fact]
    public void IsStale_ReturnsFalse_WhenFrequencyIsOnLawChange()
    {
        var p = new FormulaParameter
        {
            LastUpdatedDate = _now.AddYears(-10),
            ExpectedUpdateFrequency = "OnLawChange"
        };

        _checker.IsStale(p, _now).Should().BeFalse();
    }

    [Fact]
    public void IsStale_ReturnsTrue_ForBiannualParameter_When200DaysOld()
    {
        var p = new FormulaParameter
        {
            LastUpdatedDate = _now.AddDays(-200),
            ExpectedUpdateFrequency = "Biannual"
        };

        _checker.IsStale(p, _now).Should().BeTrue();
    }

    [Fact]
    public void IsStale_ReturnsFalse_ForBiannualParameter_When100DaysOld()
    {
        var p = new FormulaParameter
        {
            LastUpdatedDate = _now.AddDays(-100),
            ExpectedUpdateFrequency = "Biannual"
        };

        _checker.IsStale(p, _now).Should().BeFalse();
    }

    [Fact]
    public void DaysUntilStale_ReturnsNegative_WhenStale()
    {
        var p = new FormulaParameter
        {
            LastUpdatedDate = _now.AddDays(-200),
            ExpectedUpdateFrequency = "Biannual"
        };

        var days = _checker.DaysUntilStale(p, _now);

        days.Should().NotBeNull();
        days!.Value.Should().BeLessThan(0);
    }

    [Fact]
    public void DaysUntilStale_ReturnsNull_WhenStaticFrequency()
    {
        var p = new FormulaParameter
        {
            LastUpdatedDate = _now.AddDays(-100),
            ExpectedUpdateFrequency = "Static"
        };

        _checker.DaysUntilStale(p, _now).Should().BeNull();
    }
}

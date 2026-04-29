using FluentAssertions;
using LexCalculus.Core.Common;
using Xunit;

namespace LexCalculus.Tests.Common;

public class SlugHelperTests
{
    [Fact]
    public void Generate_WithTurkishCharacters_NormalizesCorrectly()
    {
        SlugHelper.Generate("Hukuk Bürosu A").Should().Be("hukuk-burosu-a");
    }

    [Fact]
    public void Generate_WithSpecialCharsAndAmpersand_DropsAndJoins()
    {
        SlugHelper.Generate("Çağdaş & Ortakları").Should().Be("cagdas-ortaklari");
    }

    [Fact]
    public void Generate_TrimsLeadingAndTrailingWhitespace()
    {
        SlugHelper.Generate("  spaces  ").Should().Be("spaces");
    }

    [Fact]
    public void Generate_EmptyInput_ReturnsEmpty()
    {
        SlugHelper.Generate("").Should().Be("");
        SlugHelper.Generate(null).Should().Be("");
        SlugHelper.Generate("   ").Should().Be("");
    }

    [Fact]
    public void Generate_CollapsesConsecutiveDashes()
    {
        SlugHelper.Generate("ABC---DEF").Should().Be("abc-def");
    }
}

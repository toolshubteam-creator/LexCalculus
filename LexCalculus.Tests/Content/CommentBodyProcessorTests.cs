using FluentAssertions;
using LexCalculus.Infrastructure.Services;
using Xunit;

namespace LexCalculus.Tests.Content;

public class CommentBodyProcessorTests
{
    private static CommentSanitizer Sanitizer() => new();

    [Fact]
    public void Process_PlainText_EscapesAndPreservesText()
    {
        var result = CommentBodyProcessor.Process("Merhaba dünya!", Sanitizer());
        result.Should().Be("Merhaba dünya!");
    }

    [Fact]
    public void Process_HtmlInjection_EscapesTags()
    {
        var input = "<script>alert('xss')</script>kötü";
        var result = CommentBodyProcessor.Process(input, Sanitizer());

        // <script> tag escape edilir; "alert" text browser tarafından render edilmez
        result.Should().NotContain("<script");
        result.Should().Contain("&lt;script");
        result.Should().Contain("&lt;/script");
        result.Should().Contain("kötü");
    }

    [Fact]
    public void Process_BoldTagAttempt_EscapesAsText()
    {
        var input = "<b>kalın</b> deneme";
        var result = CommentBodyProcessor.Process(input, Sanitizer());

        result.Should().NotContain("<b>");
        result.Should().Contain("&lt;b&gt;");
        result.Should().Contain("kalın");
    }

    [Fact]
    public void Process_LineBreaks_ConvertsToBr()
    {
        var input = "Birinci satır\nİkinci satır\nÜçüncü satır";
        var result = CommentBodyProcessor.Process(input, Sanitizer());

        result.Should().Contain("<br>");
        result.Split("<br>").Should().HaveCount(3);
    }

    [Fact]
    public void Process_Url_ConvertsToAnchorWithNofollow()
    {
        var input = "Lex Calculus: https://lexcalculus.com adresine bakın";
        var result = CommentBodyProcessor.Process(input, Sanitizer());

        result.Should().Contain("<a href=\"https://lexcalculus.com\"");
        result.Should().Contain("rel=\"nofollow noopener\"");
        result.Should().Contain("target=\"_blank\"");
    }

    [Fact]
    public void Process_MultipleUrls_ConvertsAll()
    {
        var input = "Bkz https://example.com ve https://lexcalculus.com bakın";
        var result = CommentBodyProcessor.Process(input, Sanitizer());

        result.Should().Contain("href=\"https://example.com\"");
        result.Should().Contain("href=\"https://lexcalculus.com\"");
    }

    [Fact]
    public void Process_EmptyOrWhitespace_ReturnsEmpty()
    {
        CommentBodyProcessor.Process("", Sanitizer()).Should().Be("");
        CommentBodyProcessor.Process("   ", Sanitizer()).Should().Be("");
    }

    [Fact]
    public void Process_JavascriptUrl_DoesNotCreateLink()
    {
        // Regex sadece http(s):// yakalar; javascript: scheme link olmaz
        var input = "javascript:alert(1) tıklama";
        var result = CommentBodyProcessor.Process(input, Sanitizer());

        result.Should().NotContain("href=\"javascript:");
        result.Should().Contain("javascript:alert(1)");   // text olarak kalır
    }
}

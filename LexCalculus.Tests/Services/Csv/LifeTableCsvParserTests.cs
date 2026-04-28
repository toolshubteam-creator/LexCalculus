using System.Text;
using FluentAssertions;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Services.Csv;
using Xunit;

namespace LexCalculus.Tests.Services.Csv;

public class LifeTableCsvParserTests
{
    private static Stream MakeStream(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }

    private static string Build200RowValidCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Yas,Cinsiyet,BekledigiYasam");
        for (int yas = 0; yas <= 99; yas++)
        {
            // Ortalama beklenen yaşam yaşa göre azalır; basit decreasing değerler
            var erkek = (80 - yas * 0.7m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var kadin = (84 - yas * 0.7m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"{yas},Erkek,{erkek}");
            sb.AppendLine($"{yas},Kadın,{kadin}");
        }
        return sb.ToString();
    }

    [Fact]
    public async Task ParseAsync_ValidCsv_Returns200Rows()
    {
        var csv = Build200RowValidCsv();
        using var stream = MakeStream(csv);

        var parser = new LifeTableCsvParser();
        var result = await parser.ParseAsync(stream);

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Rows.Should().HaveCount(200);
        result.Rows.Count(r => r.Cinsiyet == Cinsiyet.Erkek).Should().Be(100);
        result.Rows.Count(r => r.Cinsiyet == Cinsiyet.Kadin).Should().Be(100);
    }

    [Fact]
    public async Task ParseAsync_WrongHeader_ReturnsError()
    {
        const string csv = "InvalidHeader\n0,Erkek,80.0\n";
        using var stream = MakeStream(csv);

        var parser = new LifeTableCsvParser();
        var result = await parser.ParseAsync(stream);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].LineNumber.Should().Be(1);
        result.Errors[0].Field.Should().Be("header");
    }

    [Fact]
    public async Task ParseAsync_MissingRows_ReturnsError()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Yas,Cinsiyet,BekledigiYasam");
        for (int i = 0; i < 5; i++)
        {
            sb.AppendLine($"{i},Erkek,80.0");
            sb.AppendLine($"{i},Kadın,84.0");
        }
        using var stream = MakeStream(sb.ToString());

        var parser = new LifeTableCsvParser();
        var result = await parser.ParseAsync(stream);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("200 satır bekleniyor"));
    }

    [Fact]
    public async Task ParseAsync_DuplicateYasCinsiyet_ReturnsError()
    {
        // Geçerli 200 satır, sadece bir tanesi (Yas=30,Erkek) iki kez
        // Önce başka bir yaşı kaldırıp aynı kombinasyonu duplicate yaparak
        // total satır sayısını da bozalım — testin amacı duplicate detection
        var sb = new StringBuilder();
        sb.AppendLine("Yas,Cinsiyet,BekledigiYasam");
        sb.AppendLine("30,Erkek,50.0");
        sb.AppendLine("30,Erkek,50.5");   // duplicate (Yas=30, Erkek)
        sb.AppendLine("30,Kadın,55.0");
        using var stream = MakeStream(sb.ToString());

        var parser = new LifeTableCsvParser();
        var result = await parser.ParseAsync(stream);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "Yas+Cinsiyet" && e.Message.Contains("Yas=30"));
    }

    [Fact]
    public async Task ParseAsync_InvalidNumericValue_ReturnsError()
    {
        const string csv = "Yas,Cinsiyet,BekledigiYasam\n0,Erkek,abc\n";
        using var stream = MakeStream(csv);

        var parser = new LifeTableCsvParser();
        var result = await parser.ParseAsync(stream);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "BekledigiYasam");
    }
}

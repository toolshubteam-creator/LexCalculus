using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Tests.Integration;
using LexCalculus.Web.Areas.Admin.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Email;

[Collection("AdminWebHost")]
public class EmailTemplateRendererTests : IClassFixture<SqlServerWebApplicationFactoryFixture>
{
    private readonly SqlServerWebApplicationFactoryFixture _factory;

    public EmailTemplateRendererTests(SqlServerWebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RenderAsync_FindsAndRendersTestEmailTemplate()
    {
        // WebApplicationFactory boots the app once so razor engine + view discovery work
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();

        var model = new TestEmailModel
        {
            RecipientName = "Avukat Ahmet",
            Provider = "Logging",
            SentAt = new DateTime(2026, 4, 28, 10, 30, 0, DateTimeKind.Utc),
            MachineName = "test-host"
        };

        var html = await renderer.RenderAsync("TestEmail", model);

        html.Should().Contain("LEX CALCULUS");
        html.Should().Contain("Avukat Ahmet");
        html.Should().Contain("Logging");
        html.Should().Contain("test-host");
        html.Should().Contain("28.04.2026");
    }

    [Fact]
    public async Task RenderAsync_ThrowsWhenViewNotFound()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();

        var act = async () => await renderer.RenderAsync("NonExistentTemplate_xyz", new { Foo = 1 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NonExistentTemplate_xyz*");
    }

    // Türkçe karakter encoding'i nedeniyle assertion'lar ASCII-stable parçalar
    // üzerinden yapılır (CLAUDE.md test kuralı): model'e ASCII değerler verilir,
    // template statik metninden yalnızca ASCII-güvenli token'lar aranır.

    private IEmailTemplateRenderer ResolveRenderer()
    {
        _factory.CreateClient();
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
    }

    [Fact]
    public async Task RenderAsync_Connection_RendersPendingAndAcceptedStates()
    {
        var renderer = ResolveRenderer();

        var pending = await renderer.RenderAsync("Connection", new ConnectionEmailModel
        {
            RecipientDisplayName = "Avukat Ahmet",
            OtherDisplayName = "Selin Demir",
            IsAccepted = false,
            ProfileUrl = "https://lexcalculus.local/uye/selin-demir"
        });

        pending.Should().Contain("LEX CALCULUS");
        pending.Should().Contain("Selin Demir");
        pending.Should().Contain("https://lexcalculus.local/uye/selin-demir");
        pending.Should().NotContain("kabul");   // pending state'te "kabul etti" yok

        var accepted = await renderer.RenderAsync("Connection", new ConnectionEmailModel
        {
            RecipientDisplayName = "Avukat Ahmet",
            OtherDisplayName = "Selin Demir",
            IsAccepted = true,
            ProfileUrl = "https://lexcalculus.local/uye/selin-demir"
        });

        accepted.Should().Contain("kabul");     // accepted state ASCII anchor ("kabul etti")
    }

    [Fact]
    public async Task RenderAsync_Comment_ContainsCommenterTitleAndPreview()
    {
        var renderer = ResolveRenderer();

        var html = await renderer.RenderAsync("Comment", new CommentEmailModel
        {
            RecipientDisplayName = "Avukat Ahmet",
            CommenterDisplayName = "Selin Demir",
            PostTitle = "Kidem Tazminati Rehberi",
            CommentBodyPreview = "Cok faydali bir yazi olmus.",
            PostUrl = "https://lexcalculus.local/uye/ahmet/makale/kidem"
        });

        html.Should().Contain("Selin Demir");
        html.Should().Contain("Kidem Tazminati Rehberi");
        html.Should().Contain("Cok faydali bir yazi olmus.");
        html.Should().Contain("https://lexcalculus.local/uye/ahmet/makale/kidem");
    }

    [Fact]
    public async Task RenderAsync_ContentReport_ShowsActionAndConditionalNote()
    {
        var renderer = ResolveRenderer();

        var withNote = await renderer.RenderAsync("ContentReport", new ContentReportEmailModel
        {
            RecipientDisplayName = "Avukat Ahmet",
            ActionType = "Gizlendi",
            ContentType = "Makale",
            ContentTitle = "Ornek Baslik",
            ReviewNote = "Topluluk kurallari geregi."
        });

        withNote.Should().Contain("Makale");
        withNote.Should().Contain("gizlendi");   // ToLowerInvariant() çıktısı
        withNote.Should().Contain("Ornek Baslik");
        withNote.Should().Contain("Topluluk kurallari geregi.");
        withNote.Should().Contain("Not:");

        var withoutNote = await renderer.RenderAsync("ContentReport", new ContentReportEmailModel
        {
            RecipientDisplayName = "Avukat Ahmet",
            ActionType = "Kaldirildi",
            ContentType = "Yorum"
        });

        withoutNote.Should().NotContain("Not:");  // ReviewNote null → not bloğu render edilmez
    }

    [Fact]
    public async Task RenderAsync_MessageDigest_ListsDistinctSenders()
    {
        var renderer = ResolveRenderer();

        var html = await renderer.RenderAsync("MessageDigest", new MessageDigestEmailModel
        {
            RecipientDisplayName = "Avukat Ahmet",
            UnreadCount = 3,
            SenderDisplayNames = new[] { "Selin Demir", "Mehmet Kaya", "Selin Demir" },
            MessagesUrl = "https://lexcalculus.local/mesajlar"
        });

        html.Should().Contain("Selin Demir");
        html.Should().Contain("Mehmet Kaya");
        html.Should().Contain("https://lexcalculus.local/mesajlar");
        html.Should().Contain("3");
        // mükerrer "Selin Demir" Distinct ile tekleşir → 2 <li> girişi
        (html.Split("<li").Length - 1).Should().Be(2);
    }
}

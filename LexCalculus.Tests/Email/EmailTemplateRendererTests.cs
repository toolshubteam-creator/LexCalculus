using FluentAssertions;
using LexCalculus.Tests.Integration;
using LexCalculus.Web.Areas.Admin.Models;
using LexCalculus.Web.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Email;

[Collection("AdminWebHost")]
public class EmailTemplateRendererTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _factory;

    public EmailTemplateRendererTests(WebApplicationFactoryFixture factory)
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
}

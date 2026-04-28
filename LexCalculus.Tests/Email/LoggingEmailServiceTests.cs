using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Email;

public class LoggingEmailServiceTests
{
    [Fact]
    public async Task SendAsync_ReturnsTrue()
    {
        var service = new LoggingEmailService(NullLogger<LoggingEmailService>.Instance);
        var msg = new EmailMessage("user@example.com", "User", "Test", "<p>hi</p>");

        var result = await service.SendAsync(msg);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_HandlesNullPlainTextBody()
    {
        var service = new LoggingEmailService(NullLogger<LoggingEmailService>.Instance);
        var msg = new EmailMessage("user@example.com", null, "Test", "<p>hi</p>", PlainTextBody: null);

        var result = await service.SendAsync(msg);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_AcceptsEmptyBody()
    {
        var service = new LoggingEmailService(NullLogger<LoggingEmailService>.Instance);
        var msg = new EmailMessage("user@example.com", "User", "Test", "");

        var result = await service.SendAsync(msg);

        result.Should().BeTrue();
    }
}

using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Infrastructure.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Identity;

public class IdentityEmailSenderAdapterTests
{
    private static IdentityEmailSenderAdapter CreateAdapter(Mock<IEmailService> emailMock) =>
        new(emailMock.Object, NullLogger<IdentityEmailSenderAdapter>.Instance);

    [Fact]
    public async Task SendEmailAsync_ForwardsParametersToEmailService()
    {
        var emailMock = new Mock<IEmailService>();
        EmailMessage? captured = null;
        emailMock
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(true);

        var adapter = CreateAdapter(emailMock);
        await adapter.SendEmailAsync("test@example.com", "Konu", "<p>Body</p>");

        emailMock.Verify(
            x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
        captured.Should().NotBeNull();
        captured!.ToAddress.Should().Be("test@example.com");
        captured.Subject.Should().Be("Konu");
        captured.HtmlBody.Should().Be("<p>Body</p>");
        captured.ToDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task SendEmailAsync_EmptyEmail_DoesNotCallService()
    {
        var emailMock = new Mock<IEmailService>();
        var adapter = CreateAdapter(emailMock);

        await adapter.SendEmailAsync("", "Konu", "Body");
        await adapter.SendEmailAsync("   ", "Konu", "Body");

        emailMock.Verify(
            x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_ServiceThrows_DoesNotPropagate()
    {
        var emailMock = new Mock<IEmailService>();
        emailMock
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider down"));

        var adapter = CreateAdapter(emailMock);

        Func<Task> act = () => adapter.SendEmailAsync("test@example.com", "Konu", "Body");

        await act.Should().NotThrowAsync();
    }
}

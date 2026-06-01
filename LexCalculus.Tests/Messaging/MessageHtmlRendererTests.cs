using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Storage;
using LexCalculus.Web.Infrastructure.Rendering;
using LexCalculus.Web.Models;
using Xunit;

namespace LexCalculus.Tests.Messaging;

/// <summary>
/// Faz 6.11 (#38) — MessageHtmlRenderer VM kurma davranışı. MessagesController +
/// SignalRMessagingNotifier'dan çıkarılan ortak "Message → MessageViewModel"
/// map'inin tek-kaynak doğrulaması (saf in-memory, DB/Razor gerektirmez).
/// </summary>
public sealed class MessageHtmlRendererTests
{
    private static Message MakeMessage(int senderId, bool senderActive = true,
        string? avatarUrl = null, string displayName = "Ayşe")
    {
        return new Message
        {
            Id = 7,
            ConversationId = 3,
            SenderId = senderId,
            Body = "<p>selam</p>",
            CreatedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            IsDeleted = false,
            IsModeratorHidden = false,
            Sender = new ApplicationUser
            {
                Id = senderId,
                IsActive = senderActive,
                UserName = "ayse",
                Profile = new UserProfile { DisplayName = displayName, AvatarUrl = avatarUrl }
            }
        };
    }

    private static MessageHtmlRenderer Build(out CapturingPartialRenderer partial)
    {
        partial = new CapturingPartialRenderer();
        return new MessageHtmlRenderer(partial, new FakeMediaStorage());
    }

    [Fact]
    public void BuildViewModel_SenderIsViewer_IsOwnMessageTrue()
    {
        var renderer = Build(out _);
        var vm = renderer.BuildViewModel(MakeMessage(senderId: 42), viewerId: 42);

        vm.IsOwnMessage.Should().BeTrue();
        vm.Id.Should().Be(7);
        vm.ConversationId.Should().Be(3);
        vm.SenderDisplayName.Should().Be("Ayşe");
    }

    [Fact]
    public void BuildViewModel_SenderNotViewer_IsOwnMessageFalse()
    {
        var renderer = Build(out _);
        var vm = renderer.BuildViewModel(MakeMessage(senderId: 42), viewerId: 99);

        vm.IsOwnMessage.Should().BeFalse("alıcı perspektifi — gönderen karşı taraf");
    }

    [Fact]
    public void BuildViewModel_AnonymizedSender_AnonNameAndNoAvatar()
    {
        var renderer = Build(out _);
        var vm = renderer.BuildViewModel(
            MakeMessage(senderId: 5, senderActive: false, avatarUrl: "uploads/avatars/5/x.webp"),
            viewerId: 9);

        vm.SenderDisplayName.Should().Be("Silinmiş Kullanıcı");
        vm.SenderAvatarUrl.Should().BeNull("inactive/anonimize gönderen avatarı gizli");
    }

    [Fact]
    public void BuildViewModel_ActiveSenderWithAvatar_PublicUrlBuilt()
    {
        var renderer = Build(out _);
        var vm = renderer.BuildViewModel(
            MakeMessage(senderId: 5, avatarUrl: "uploads/avatars/5/x.webp"), viewerId: 9);

        vm.SenderAvatarUrl.Should().Be("/uploads/avatars/5/x.webp", "FakeMediaStorage '/' prefix ekler");
    }

    [Fact]
    public async Task RenderForViewerAsync_RendersMessagePartialWithBuiltVm()
    {
        var renderer = Build(out var partial);
        var html = await renderer.RenderForViewerAsync(MakeMessage(senderId: 42), viewerId: 42);

        html.Should().Be("<rendered/>");
        partial.LastViewName.Should().Be("_Message");
        partial.LastModel.Should().BeOfType<MessageViewModel>()
            .Which.IsOwnMessage.Should().BeTrue();
    }

    private sealed class CapturingPartialRenderer : IPartialRenderer
    {
        public string? LastViewName { get; private set; }
        public object? LastModel { get; private set; }

        public Task<string> RenderAsync<TModel>(string viewName, TModel model, CancellationToken ct = default)
        {
            LastViewName = viewName;
            LastModel = model;
            return Task.FromResult("<rendered/>");
        }
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> StoreAsync(Stream content, string subdirectory,
            string fileName, CancellationToken ct = default) => Task.FromResult($"{subdirectory}/{fileName}");
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}

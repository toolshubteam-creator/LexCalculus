using System.Text.RegularExpressions;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexCalculus.Infrastructure.Email;

/// <summary>
/// INotificationEmailDispatcher implementasyonu. Sosyal bildirim tiplerini
/// Views/Emails/ template'leriyle render edip IEmailService ile gönderir.
/// Master + granüler + anonimize kontrolü; entity reload ile yapısal model dolgusu.
/// Faz 6.2 P2.
/// </summary>
public sealed class NotificationEmailDispatcher : INotificationEmailDispatcher
{
    private readonly ApplicationDbContext _ctx;
    private readonly IEmailService _email;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly string _siteUrl;
    private readonly ILogger<NotificationEmailDispatcher> _logger;

    public NotificationEmailDispatcher(
        ApplicationDbContext ctx,
        IEmailService email,
        IEmailTemplateRenderer renderer,
        IOptions<SeoSettings> seo,
        ILogger<NotificationEmailDispatcher> logger)
    {
        _ctx = ctx;
        _email = email;
        _renderer = renderer;
        _siteUrl = (seo.Value?.SiteUrl ?? "https://lexcalculus.com").TrimEnd('/');
        _logger = logger;
    }

    public async Task DispatchAsync(Notification notification, CancellationToken ct = default)
    {
        try
        {
            var user = await _ctx.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, ct);

            // Anonimize / pasif / e-postasız kullanıcıya gönderme (Adım 5.1 entegrasyonu)
            if (user is null || !user.IsActive || string.IsNullOrEmpty(user.Email)) return;
            var profile = user.Profile;
            if (profile is null) return;

            // MASTER switch
            if (!user.NotificationsEmailEnabled) return;

            // GRANÜLER kategori opt-in
            var granularOn = notification.Type switch
            {
                NotificationType.ConnectionRequest or NotificationType.ConnectionAccepted => profile.EmailOnConnection,
                NotificationType.PostComment => profile.EmailOnComment,
                NotificationType.ContentHidden or NotificationType.ContentRestored
                    or NotificationType.ContentRemoved or NotificationType.ContentReportResolved => profile.EmailOnContentReport,
                _ => false
            };
            if (!granularOn) return;

            var recipientName = string.IsNullOrEmpty(profile.DisplayName)
                ? (user.UserName ?? "")
                : profile.DisplayName;

            switch (notification.Type)
            {
                case NotificationType.ConnectionRequest:
                case NotificationType.ConnectionAccepted:
                    await DispatchConnectionAsync(user, recipientName, notification, ct);
                    break;
                case NotificationType.PostComment:
                    await DispatchCommentAsync(user, recipientName, notification, ct);
                    break;
                default:
                    await DispatchContentReportAsync(user, recipientName, notification, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Best-effort: notification kaydı zaten var, e-posta hatası akışı bozmaz.
            _logger.LogError(ex,
                "Bildirim e-postası gönderilemedi: notif={Id} type={Type}",
                notification.Id, notification.Type);
        }
    }

    private async Task DispatchConnectionAsync(
        ApplicationUser user, string recipientName, Notification n, CancellationToken ct)
    {
        if (n.RelatedEntityId is not int connId) return;
        var conn = await _ctx.UserConnections.FirstOrDefaultAsync(c => c.Id == connId, ct);
        if (conn is null) return;

        var otherId = conn.RequesterId == user.Id ? conn.TargetId : conn.RequesterId;
        var other = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == otherId, ct);
        var otherName = string.IsNullOrEmpty(other?.DisplayName) ? "Bir kullanıcı" : other!.DisplayName;
        var profileUrl = other?.PublicSlug is { Length: > 0 } slug
            ? $"{_siteUrl}/uye/{slug}"
            : $"{_siteUrl}/baglantilarim";

        var isAccepted = n.Type == NotificationType.ConnectionAccepted;
        var model = new ConnectionEmailModel
        {
            RecipientDisplayName = recipientName,
            OtherDisplayName = otherName,
            IsAccepted = isAccepted,
            ProfileUrl = profileUrl
        };
        var html = await _renderer.RenderAsync("Connection", model, ct);
        var subject = isAccepted ? "Bağlantı isteğiniz kabul edildi" : "Yeni bağlantı isteği";
        await _email.SendAsync(new EmailMessage(user.Email!, recipientName, subject, html), ct);
    }

    private async Task DispatchCommentAsync(
        ApplicationUser user, string recipientName, Notification n, CancellationToken ct)
    {
        if (n.RelatedEntityId is not int commentId) return;
        var comment = await _ctx.PostComments.FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null) return;

        var post = await _ctx.UserPosts.FirstOrDefaultAsync(p => p.Id == comment.PostId, ct);
        var commenter = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == comment.UserId, ct);

        var model = new CommentEmailModel
        {
            RecipientDisplayName = recipientName,
            CommenterDisplayName = string.IsNullOrEmpty(commenter?.DisplayName) ? "Bir kullanıcı" : commenter!.DisplayName,
            PostTitle = string.IsNullOrEmpty(post?.Title) ? "Makaleniz" : post!.Title,
            CommentBodyPreview = BuildPreview(comment.Body),
            PostUrl = BuildAbsolute(n.Link)
        };
        var html = await _renderer.RenderAsync("Comment", model, ct);
        await _email.SendAsync(new EmailMessage(user.Email!, recipientName, "Makalenize yeni yorum", html), ct);
    }

    private async Task DispatchContentReportAsync(
        ApplicationUser user, string recipientName, Notification n, CancellationToken ct)
    {
        // Yalnızca içerik SAHİBİ bildirimleri (relatedEntityType = Post/Comment/Message).
        // Reporter bildirimleri relatedEntityType = "ContentReport" → e-posta gönderilmez.
        var contentType = n.RelatedEntityType switch
        {
            "Post" => "Makale",
            "Comment" => "Yorum",
            "Message" => "Mesaj",
            _ => null
        };
        if (contentType is null) return;

        var actionType = n.Type switch
        {
            NotificationType.ContentHidden => "Gizlendi",
            NotificationType.ContentRemoved => "Kaldırıldı",
            NotificationType.ContentRestored => "Geri yüklendi",
            _ => "İncelendi"
        };

        var model = new ContentReportEmailModel
        {
            RecipientDisplayName = recipientName,
            ActionType = actionType,
            ContentType = contentType,
            ContentTitle = null,
            ReviewNote = null
        };
        var html = await _renderer.RenderAsync("ContentReport", model, ct);
        await _email.SendAsync(
            new EmailMessage(user.Email!, recipientName, $"İçeriğiniz {actionType.ToLowerInvariant()}", html), ct);
    }

    private static string BuildPreview(string? body)
    {
        var text = Regex.Replace(body ?? "", "<.*?>", "").Trim();
        return text.Length > 200 ? text[..200] + "…" : text;
    }

    private string BuildAbsolute(string? link)
    {
        if (string.IsNullOrEmpty(link)) return _siteUrl;
        if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return link;
        return link.StartsWith('/') ? $"{_siteUrl}{link}" : $"{_siteUrl}/{link}";
    }
}

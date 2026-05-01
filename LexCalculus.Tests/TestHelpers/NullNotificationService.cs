using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Notifications;

namespace LexCalculus.Tests.TestHelpers;

/// <summary>
/// Test-only no-op INotificationService — testin asıl ilgilendiği davranış
/// notification değilse bu kullanılır. Notification davranışını assert eden
/// testler RecordingNotificationService kullanmalı.
/// </summary>
internal sealed class NullNotificationService : INotificationService
{
    public Task<Notification?> CreateAsync(
        NotificationType type, int userId, string title, string body,
        string? link = null, string? relatedEntityType = null,
        int? relatedEntityId = null, string? iconHint = null,
        TimeSpan? dedupWindow = null, CancellationToken ct = default)
        => Task.FromResult<Notification?>(null);

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId, int limit, bool unreadOnly, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Notification>>(Array.Empty<Notification>());

    public Task MarkAsReadAsync(int notificationId, int userId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<int> MarkAllAsReadAsync(int userId, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<int> GetTotalActiveCountAsync(CancellationToken ct = default)
        => Task.FromResult(0);
}

/// <summary>
/// Test-only INotificationService — CreateAsync çağrılarını listede tutar,
/// notification entegrasyon testleri için. Get* metotları no-op.
/// </summary>
internal sealed class RecordingNotificationService : INotificationService
{
    public List<RecordedNotification> Created { get; } = new();

    public Task<Notification?> CreateAsync(
        NotificationType type, int userId, string title, string body,
        string? link = null, string? relatedEntityType = null,
        int? relatedEntityId = null, string? iconHint = null,
        TimeSpan? dedupWindow = null, CancellationToken ct = default)
    {
        Created.Add(new RecordedNotification(type, userId, title, body, link,
            relatedEntityType, relatedEntityId));
        return Task.FromResult<Notification?>(new Notification
        {
            Id = Created.Count, UserId = userId, Type = type,
            Title = title, Body = body, Link = link,
            RelatedEntityType = relatedEntityType, RelatedEntityId = relatedEntityId,
            CreatedAt = DateTime.UtcNow
        });
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
        => Task.FromResult(Created.Count(n => n.UserId == userId));

    public Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId, int limit, bool unreadOnly, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Notification>>(Array.Empty<Notification>());

    public Task MarkAsReadAsync(int notificationId, int userId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<int> MarkAllAsReadAsync(int userId, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<int> GetTotalActiveCountAsync(CancellationToken ct = default)
        => Task.FromResult(Created.Count);
}

internal sealed record RecordedNotification(
    NotificationType Type, int UserId, string Title, string Body,
    string? Link, string? RelatedEntityType, int? RelatedEntityId);

using LexCalculus.Core.Entities.Notifications;

namespace LexCalculus.Web.Models.Notifications;

public sealed class NotificationsListViewModel
{
    public required IReadOnlyList<Notification> Items { get; init; }
    public bool UnreadOnly { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

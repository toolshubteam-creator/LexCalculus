using LexCalculus.Core.Entities.Notifications;

namespace LexCalculus.Core.Email;

/// <summary>
/// In-app bildirim oluşturulduğunda (sosyal tipler) ilgili e-postayı gönderir.
/// Master switch (ApplicationUser.NotificationsEmailEnabled) + granüler tercih
/// (UserProfile.EmailOn*) + anonimize (IsActive) kontrolü içerir. Best-effort:
/// hata fırlatmaz (notification kaydı korunur). Faz 6.2 P2, #22, charter §3 Karar 3.
/// </summary>
public interface INotificationEmailDispatcher
{
    Task DispatchAsync(Notification notification, CancellationToken ct = default);
}

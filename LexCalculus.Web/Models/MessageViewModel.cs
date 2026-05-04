namespace LexCalculus.Web.Models;

/// <summary>
/// /mesajlar/{id} sayfasında ve _Message partial'da mesaj kartı için VM.
/// IsOwnMessage server tarafında hesaplanır → JS yalnızca render eder.
/// Faz 5.5.
/// </summary>
public sealed class MessageViewModel
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }

    /// <summary>Anonimize/IsActive=false durumunda 'Silinmiş Kullanıcı'.</summary>
    public string SenderDisplayName { get; set; } = "";

    /// <summary>IsActive=false ise null (avatar gizli).</summary>
    public string? SenderAvatarUrl { get; set; }

    /// <summary>Sanitize edilmiş HTML — view'da Html.Raw güvenli.</summary>
    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Faz 5.7 — admin moderasyon gizleme. View IsDeleted/IsModeratorHidden
    /// koşullarına göre body yerine placeholder render eder. Sahip kendi gizli
    /// mesajını "(Yönetim tarafından gizlendi)" placeholder olarak görür;
    /// recipient için MessageService zaten liste'den filter eder.
    /// </summary>
    public bool IsModeratorHidden { get; set; }

    /// <summary>Viewer kendi mesajı mı (sağ alignment + Sil butonu).</summary>
    public bool IsOwnMessage { get; set; }
}

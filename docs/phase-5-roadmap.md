# Faz 5 — Real-time + Olgunlaştırma + KVKK

**Başlangıç:** 2 Mayıs 2026
**Tahmini süre:** ~6 hafta (3 dalga, 9 alt adım)
**Charter:** [docs/phase-5-charter.md](./phase-5-charter.md)
**Tag (kapanış):** `phase-5-complete`

## Durum

🟢 **Dalga A tamamlandı (3 Mayıs 2026)** — Tag: `phase-5-wave-a-complete`
🟡 **Dalga B (Mesajlaşma) sırada** — 5.4-5.7

## Dalga Yapısı

| Dalga | Konu | Adım Aralığı | Tahmin | Durum |
|---|---|---|---|---|
| A | Olgunlaştırma (KVKK + güvenlik) | 5.1 - 5.3 | 1.5-2 hafta → ~1 gün | ✅ |
| B | Mesajlaşma altyapısı (Conversation + SignalR) | 5.4 - 5.7 | 3 hafta | ⏳ |
| C | Test infrastructure + Faz closeout | 5.8 - 5.9 | 1 hafta | ⏳ |

## Adımlar

| Adım | Konu | Bağımlılık | Charter Karar | Durum |
|---|---|---|---|---|
| 5.1 | Hesap silme + anonimize stratejisi | Faz 4 FK Restrict yapısı | 6 | ✅ |
| 5.2 | Rate limiting middleware | — | 7 | ✅ |
| 5.3 | Hide moderation + NoIndex auto | Adım 4.10 P2 | 11, 12 | ✅ |
| 5.4 | Conversation + Message entity + servis | Adım 4.2 (UserConnection), 4.3 (UserBlock) | 1, 4, 5 | ⏳ |
| 5.5 | Mesajlaşma UI (polling fallback) | 5.4 | 9 | ⏳ |
| 5.6 | SignalR real-time entegrasyonu | 5.5 | 2, 8 | ⏳ |
| 5.7 | Mesaj moderasyon + Notification + Dalga B closeout | 5.4-5.6 | 3 | ⏳ |
| 5.8 | SQL Server LocalDB test migration | — (paralel olabilir) | 10 | ⏳ |
| 5.9 | Faz 5 closeout (roadmap, README, tech-debt, tag) | tüm | — | ⏳ |

---

## Adım Detayları

### Adım 5.1 — Hesap silme + anonimize

**Önkoşul:** Faz 4 tamamlandı (`phase-4-complete` tag).

**Kapsam:**
- `UserAdminService.AnonymizeAsync` (atomik: User + UserProfile)
- `/profil/hesabi-sil` form (email onay zorunlu, 7-gün undo penceresi)
- `User.AnonymizeStatus` enum (Active, PendingAnonymize, Anonymized)
- Hangfire recurring job: `AnonymizePendingAccountsJob` (günlük; 7+ gün
  PendingAnonymize → Anonymized)
- ActivityLog: `User.RequestAnonymize`, `User.CancelAnonymize`,
  `User.Anonymize`
- Public profile (`/uye/{slug}`) Anonymized → 404
- Tüm view component'lerde "Silinmiş Kullanıcı" placeholder

**Charter Karar:** 6

**Süre:** ~3-4 gün

### Adım 5.2 — Rate limiting middleware

**Kapsam:**
- `Microsoft.AspNetCore.RateLimiting` middleware register
- Per-user fixed window policies (yorum, şikayet, mesaj, bağlantı, AJAX)
- 429 yanıt: JSON body + `Retry-After` header
- Test: `RateLimitTests` — 11. istek 429 + Retry-After
- Admin bypass: `[DisableRateLimiting]` admin endpoint'lerde

**Charter Karar:** 7

**Süre:** ~2 gün

### Adım 5.3 — Hide moderation + NoIndex auto

**Kapsam:**
- `UserPost.IsModeratorHidden` + `PostComment.IsModeratorHidden` migration
- Public query filter (`HasQueryFilter` merkezi)
- Sahip bypass: makale/yorum sahibi gizli içeriği "Yönetim tarafından
  gizlendi" placeholder ile görür
- Admin paneli (`/admin/sikayetler/{type}/{id}` Detail): "Gizle" butonu
  + "Sil" butonu yan yana
- `_Layout.cshtml` otomatik NoIndex: `[Authorize]` veya admin area
- ActivityLog: `Post.AdminHide`, `Post.AdminUnhide`,
  `Comment.AdminHide`, `Comment.AdminUnhide`

**Charter Karar:** 11, 12

**Süre:** ~3-4 gün

### Adım 5.4 — Conversation + Message entity + servis

**Kapsam:**
- `Conversation` entity (ParticipantAUserId, ParticipantBUserId,
  LastMessageAt, IsArchived flags)
- `Message` entity (ConversationId, SenderId, Body, IsDeleted,
  CreatedAt)
- `IConversationService` (Create, GetForUser, GetMessages,
  ArchiveForUser)
- `IMessageService` (Send, MarkAsRead, DeleteForSender)
- Engelleme entegrasyonu (Karar 4): mesaj atma server-side blokaj
- Migration: `AddMessaging`

**Charter Karar:** 1, 4, 5

**Süre:** ~4-5 gün

### Adım 5.5 — Mesajlaşma UI (polling fallback önce)

**Kapsam:**
- `/mesajlar` Razor Page (conversation listesi)
- `/mesajlar/{conversationId}` mesaj geçmişi + gönderme alanı
- AJAX polling 5 saniye (SignalR öncesi fallback)
- Profil sayfası "Mesaj At" butonu (Karar 3 yetkisi varsa)
- Mobile responsive (sol panel ↔ sağ panel)

**Charter Karar:** 9

**Süre:** ~3-4 gün

### Adım 5.6 — SignalR real-time entegrasyonu

**Kapsam:**
- `LexCalculus.Web/Hubs/MessagingHub` ([Authorize])
- JS client (cookie auth + auto-reconnect)
- Polling kaldırıldı, real-time akış
- Yeni mesaj geldiğinde bell icon güncelleme (SignalR → bell ViewComponent
  refresh event)
- Manual test: çift sekme, real-time akış

**Charter Karar:** 2, 8

**Süre:** ~3-4 gün

### Adım 5.7 — Mesaj moderasyon + Notification + Dalga B closeout

**Kapsam:**
- `NotificationType.Message` (102'den sonra yeni int — örn. 106)
- ConversationService → NotificationService entegrasyon
- Kullanıcı tercihi: "Her mesaj için bildir" / "Sessiz" / "Toplu özet"
- Mesaj içerik şikayet (ContentReportTargetType.Message ekle)
- Manuel doğrulama (Dalga B senaryolar)
- Mini tag: `phase-5-wave-b-complete`

**Charter Karar:** 3

**Süre:** ~2-3 gün

### Adım 5.8 — SQL Server LocalDB test migration

**Kapsam:**
- `SqlDbFixture` (xUnit IClassFixture, random GUID database)
- Mevcut `TestDbContextFactory` SQL Server'a geç (InMemory'den)
- Mevcut testleri pas et (provider abstraction)
- GitHub Actions service container (`mssql/server:2022-latest`)
- `[Trait("Provider","Sql")]` opsiyonel etiketleme

**Charter Karar:** 10

**Süre:** ~3-4 gün (paralel ile diğer adımlarla)

### Adım 5.9 — Faz 5 closeout

**Kapsam:**
- Manuel doğrulama (tüm Faz 5 senaryoları)
- README'de Faz 5 final özeti
- `docs/phase-5-roadmap.md` (bu dosya) güncellemesi (5.1-5.9 ✅)
- `docs/tech-debt.md` Faz 5'te kapatılan maddeler "ÇÖZÜLDÜ" notu
- Faz 6 önizleme notu charter'a
- `phase-5-complete` annotated tag

**Süre:** ~1 gün

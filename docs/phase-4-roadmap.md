# Faz 4 — Sosyal Platform

**Başlangıç:** 30 Nisan 2026
**Bitiş:** 2 Mayıs 2026 (Dalga A + B; Dalga C → Faz 5)
**Tahmini süre:** ~14 hafta (3 dalga, orijinal)
**Gerçek süre:** ~2 gün (Dalga A + B)
**Charter:** [docs/phase-4-charter.md](./phase-4-charter.md)
**Tag (kapanış):** `phase-4-complete`

## Durum

🟢 **Dalga A tamamlandı (1 Mayıs 2026)** — Tag: `phase-4-wave-a-complete`
🟢 **Dalga B tamamlandı (2 Mayıs 2026)** — Tag: `phase-4-wave-b-complete`
🟢 **Faz 4 kapandı (2 Mayıs 2026)** — Tag: `phase-4-complete`
↪️ **Dalga C (mesajlaşma 4.12-4.14) → Faz 5'e taşındı** (kapsam yoğunluğu)

## Dalga Yapısı

| Dalga | Konu | Adım Aralığı | Süre (tahmin → gerçek) | Durum |
|---|---|---|---|---|
| A | Profil + Bağlantı + Engelleme + Notification | 4.1 - 4.4 | ~3 hafta → ~1 gün | ✅ |
| B | UGC: Makale + Yorum + Beğeni + Şikayet + Moderasyon | 4.5 - 4.11 | ~6 hafta → ~1 gün | ✅ |
| C | Mesajlaşma: SignalR + Conversation + Messages | 4.12 - 4.14 | ~3 hafta | ↪️ Faz 5 |

## Adımlar

| Adım | Konu | Durum |
|---|---|---|
| 4.1  | Public profile UI binding | ✅ |
| 4.2  | UserConnections altyapısı (P1+P2+P3a+P3b) | ✅ |
| 4.3  | UserBlock + Notification genişletme | ✅ |
| 4.4  | Dalga A closeout | ✅ (4.3 ile birleştirildi) |
| 4.5  | PostCategory + PostTag altyapısı | ✅ |
| 4.6  | UserPost entity + temel CRUD + editör | ✅ |
| 4.7  | Public makale görüntüleme + SEO | ✅ |
| 4.8  | Görsel yükleme altyapısı (featured + inline + AJAX + WebP) | ✅ |
| 4.9  | PostComment + PostLike | ✅ |
| 4.10 | Şikayet sistemi + admin moderasyon | ✅ |
| 4.11 | Dalga B closeout + Faz 4 kapanışı | ✅ |
| 4.12 | SignalR altyapı + Conversation/Message entity'leri | ↪️ Faz 5 |
| 4.13 | Mesajlaşma UI (real-time) | ↪️ Faz 5 |
| 4.14 | Faz 4 closeout (Notification.Message + bell + tag) | ↪️ Faz 5 |

## Dalga C → Faz 5

Charter §2.3'te tanımlanan mesajlaşma katmanı (4.12-4.14) Faz 5'e
taşındı. Gerekçe: Dalga A + B ile sosyal yüzey + UGC tamamlandı,
mesajlaşma bağımsız bir kapsam — Faz 5 başlığı altında diğer
real-time + advanced UGC iyileştirmeleriyle birlikte ele alınması
mantıklı.

Faz 5 başlığı (öneri): **"Real-time + advanced UGC + KVKK"**

İçerik (önizleme):
- 4.12 Direct Messaging entity + servis (Conversation, Message)
- 4.13 SignalR + real-time UI
- 4.14 Mesajlaşma altyapısı kapanış (retention, search, moderation)
- Faz 4 Dalga B tech-debt sıraya alınır (madde 11-23)

Süre tahmini Faz 5 başında ölçülür; Faz 4 tahmin/gerçek oranı (13 hafta
→ 2 gün, ~30x) yanıltıcı olabilir, Faz 5 kendi ölçümüyle plan yapılacak.

## Adım Detayları

### Adım 4.1 — Public profile UI binding

**Önkoşul:** Faz 3 tamamlandı (`phase-3-complete` tag).

**Kapsam:**
- `/profil` sayfasında IsPublicProfile + ShowTenant + Bio + Avatar + City alanlarının UI'a bağlanması
- `/uye/{slug}` route + ProfilController.Goruntule action
- Person JSON-LD (LD+JSON view component genişletme)
- Sitemap entegrasyonu (DB-driven public profile URL'leri)
- Avatar upload (görsel altyapısı temeli — MediaFile entity sade hâli)

**Karar bağı:** Karar 1 (iki ayrı toggle), Karar 14 (avatar = aynı görsel altyapı)

**Süre:** ~5-7 gün

### Adım 4.2 — UserConnections altyapısı

**Kapsam:**
- UserConnection entity + migration (RequesterId, TargetId, Status, CreatedAt, RespondedAt)
- ConnectionService (TenantInvitation pattern reuse)
- "Bağlantı İste" UI (profil sayfasında)
- `/baglantilarim` sayfası (sekme: bekleyen, aktif, gönderdiklerim)
- Audit log entegrasyonu (UserConnection.Send/Accept/Reject/Cancel)

**Karar bağı:** Karar 2 (LinkedIn modeli), Karar 20 (state machine reuse)

**Süre:** ~5-7 gün

### Adım 4.3 — UserBlock + Notification genişletme

**Kapsam:**
- UserBlock entity + servisi
- "Engelle" butonu (profil sayfasında)
- Engellenen kullanıcı görünürlük kuralları (profil/bağlantı/mesaj)
- NotificationType enum'a `Connection`, `ConnectionAccepted` ekle
- ConnectionService → NotificationService entegrasyon
- Bell icon UI test (yeni event türleri görünüyor mu)

**Karar bağı:** Karar 5 (UserBlock var), Karar 19 (Notification genişletme)

**Süre:** ~3-5 gün

### Adım 4.4 — Dalga A closeout

**Kapsam:**
- Manuel doğrulama (tüm Dalga A senaryoları)
- README'de Dalga A özeti
- `docs/phase-4-roadmap.md` güncellemesi (4.1-4.4 ✅)
- Mini tag: `phase-4-wave-a-complete` (annotated)

**Süre:** ~1 gün

### Adım 4.5 — PostCategory + PostTag altyapısı

**Kapsam:**
- PostCategory entity (admin yönetimli master list)
- PostTag entity + popüler tag query (UsageCount sıralı)
- `Areas/Admin/Controllers/PostCategoriesController`
- Tag yönetimi servis (otomatik UsageCount artışı)

**Karar bağı:** Karar 12 (karma tag stratejisi)

**Süre:** ~3-5 gün

### Adım 4.6 — UserPost entity + temel CRUD + editör

**Kapsam:**
- UserPost entity + PostTagLink + migration
- Slug üretimi: `/uye/{user-slug}/makale/{post-slug}`
- UserPostService (taslak/yayında, slug üretimi)
- `/makalelerim` sayfası
- Makale yazma editörü (rich text — TinyMCE veya Quill seçimi adım başında)
- Otomatik footer disclaimer

**Karar bağı:** Karar 8 (disclaimer), Karar 9 (taslak/yayında), Karar 11 (slug)

**Süre:** ~7-10 gün (büyük adım, parçalara bölünebilir)

### Adım 4.7 — Public makale görüntüleme + SEO

**Kapsam:**
- `/uye/{user-slug}/makale/{post-slug}` route + controller
- MakaleController.Goruntule action
- Article JSON-LD + Open Graph
- Sitemap entegrasyonu (public makale URL'leri)
- View count increment (atomik, ExecuteUpdate)

**Karar bağı:** Karar 11 (slug), 5.2 risk (view count race)

**Süre:** ~4-5 gün

### Adım 4.8 — Görsel yükleme altyapısı

**Kapsam:**
- MediaFile entity tam hâli
- IMediaUploadService (5 MB max, JPG/PNG/WebP, MIME re-validation, EXIF strip)
- Editör entegrasyonu (drag & drop)
- Yerel disk path stratejisi (`wwwroot/uploads/posts/{userId}/{guid}.{ext}`)
- Avatar yükleme bu altyapıyı kullanır (4.1'de stub vardıysa burada tamamlanır)

**Karar bağı:** Karar 13, 14, 5.2 risk (görsel güvenliği)

**Süre:** ~4-6 gün

### Adım 4.9 — PostComment + PostLike

**Kapsam:**
- PostComment entity (hierarchical, ParentCommentId)
- PostLike entity
- PostCommentService + PostLikeService
- Yorum UI (makale altında, reply nested)
- Beğeni toggle UI
- NotificationType.`Comment`, `.CommentReply`, `.Like` ekle
- Sahip moderasyon (kendi makalesindeki yorumu silme)

**Karar bağı:** Karar 6 (sahip moderasyon), Karar 19

**Süre:** ~5-7 gün

### Adım 4.10 — Şikayet sistemi + admin moderasyon

**Kapsam:**
- ContentReport entity (TargetType: Post/Comment, Status: Pending/Resolved/Dismissed)
- "Bildir" butonu (post + comment için)
- `Areas/Admin/Controllers/ContentReportsController`
- Admin `/admin/sikayetler` — onay sırası
- Admin müdahale: içerik kaldırma + kullanıcı askıya alma
- NotificationType.`Reported` (sahibe), `.ContentRemoved` (sahibe)
- Unique constraint: ReporterId + TargetType + TargetId

**Karar bağı:** Karar 6, 7, 5.2 risk (şikayet spam)

**Süre:** ~5-7 gün

### Adım 4.11 — Dalga B closeout + Faz 4 kapanışı ✅

**Kapsam:**
- Manuel doğrulama (Dalga A + B senaryoları integration testlerle)
- README'de Dalga B + Faz 4 final özeti
- `docs/phase-4-roadmap.md` (bu dosya) güncellemesi
- `docs/tech-debt.md` Dalga B'de keşfedilen 13 madde (11-23)
- `docs/phase-4-charter.md` Implementation Status notu
- 2 annotated tag: `phase-4-wave-b-complete` + `phase-4-complete`
- Dalga C → Faz 5 taşıma kararı belgelendi

**Süre:** ~1 gün (gerçek)

### Adım 4.12 — SignalR altyapı + entity'ler (↪️ Faz 5)

**Kapsam:**
- SignalR NuGet (`Microsoft.AspNetCore.SignalR`)
- `LexCalculus.Web/Hubs/MessagingHub`
- Conversation + Message entity + migration
- ConversationService (engelleme entegrasyonu — Karar 17)
- Hub authentication (cookie auth tabanlı)

**Karar bağı:** Karar 15, 16, 17, 5.2 risk (SignalR scale-out)

**Süre:** ~5-7 gün

### Adım 4.13 — Mesajlaşma UI (real-time) (↪️ Faz 5)

**Kapsam:**
- `/mesajlar` sayfası (sohbet listesi + aktif sohbet panel)
- Real-time mesaj gönderme/alma (SignalR client)
- Okundu indikatörü + zaman damgası
- "Profilden mesaj at" entegrasyonu (public profile sayfası)
- Engelleme race koruması (server-side son kontrol)

**Karar bağı:** Karar 17, 5.2 risk (real-time UI senkron)

**Süre:** ~5-7 gün

### Adım 4.14 — Faz 4 closeout (↪️ Faz 5)

**Kapsam:**
- NotificationType.`Message` ekle
- ConversationService → NotificationService entegrasyon
- Bell icon mesaj event'i test
- README Faz 4 closeout bölümü
- `docs/phase-4-roadmap.md` tüm adımlar ✅
- `docs/tech-debt.md` Faz 4 maddeleri ekle
- `phase-4-complete` annotated tag
- Push origin main + tags

**Süre:** ~1-2 gün

# Lex Calculus

Türk hukukuna özgü hesaplamalar için profesyonel SaaS platformu. Avukatlar ve bilirkişiler için tasarlanmış.

## Durum

**Faz 6 — Olgunlaştırma + UX + Performance başladı (29 Mayıs 2026).**
[Charter](./docs/phase-6-charter.md) · [Roadmap](./docs/phase-6-roadmap.md) · [Kapsam envanteri](./docs/phase-6-scope-inventory.md)

**Faz 5 — Real-time + Olgunlaştırma + KVKK tamamlandı (15 Mayıs 2026).**
[Charter](./docs/phase-5-charter.md) · [Roadmap](./docs/phase-5-roadmap.md)

- 17/17 hesaplayıcı aktif
- 779/779 test geçiyor
- Public profil + bağlantı + engelleme + notification (Faz 4 Dalga A)
- Kullanıcı içerik üretimi: makale + yorum + beğeni + şikayet + admin moderasyon (Faz 4 Dalga B)
- 1-1 mesajlaşma: SignalR real-time + 30 sn polling fallback + moderasyon (Faz 5 Dalga B)
- KVKK: hesap silme/anonimize, rate limiting, hide moderation (Faz 5 Dalga A)
- Görsel altyapısı: featured + inline image, AJAX upload, ImageSharp + WebP
- Admin paneli: parametre/LifeTable/kullanıcı/tenant/talep/davet/kategori/şikayet (Post+Comment+Message) CRUD, activity log
- Multi-tenant altyapı (hukuk büroları); bireysel vatandaş kullanıcıları etkilenmiyor (TenantId nullable)
- 2 Hangfire recurring job (veri tazelik kontrolü, davet expire)
- 9 hukuk kategorisinden 3'ü (İş Hukuku, Aktüerya, Faiz) hesaplayıcı olarak hazır
- Kalan 6 hukuk kategorisi (Vergi, Aile, Miras, vb.) Faz 6+'da eklenecek

## Hesaplayıcılar

### İş Hukuku (7/7)
- Kıdem Tazminatı (1475 s.K. m.14)
- İhbar Tazminatı (4857 s.K. m.17)
- Yıllık İzin Ücreti (4857 s.K. m.53)
- Fazla Mesai Alacağı (4857 s.K. m.41)
- İşe İade Tazminatı (4857 s.K. m.18-21)
- Asgari Ücret Uyumluluk Kontrolü (4857 s.K.)
- Mobbing / Manevi Tazminat (TBK m.58 + 4857 s.K. m.5,77)

### Aktüerya (5/5)
- Destekten Yoksun Kalma (TBK m.53 + Yargıtay HGK)
- Maluliyet Tazminatı (TBK m.54 + 5510 s.K.)
- Geçici İş Göremezlik (5510 s.K. m.18 / TBK m.54)
- Bakıcı Gideri (TBK m.54)
- Araç Değer Kaybı (TRAMER baseline)

### Faiz (5/5)
- Yasal Faiz (3095 s.K. m.1)
- Ticari Temerrüt Faizi (3095 s.K. m.2)
- Akdî Temerrüt Faizi (TBK m.120)
- Kira Artışı (TBK m.344)
- Menfi Tespit Faizi (TBK m.78-79)

## Mimari

ASP.NET Core MVC (.NET 10), 5 katmanlı solution:
- **LexCalculus.Core** — Domain entities, calculator interfaces, DTO'lar
- **LexCalculus.Infrastructure** — EF Core, services, repositories, calculator implementasyonları
- **LexCalculus.Web** — MVC controllers, views, statik kaynaklar
- **LexCalculus.Tests** — xUnit testleri (219/219)
- **LexCalculus.Shared** — Cross-cutting tipler

### Önemli mimari kararlar

- **Identity:** ASP.NET Core Identity, `ApplicationUser : IdentityUser<int>`. Audit interceptor ile CreatedAt/UpdatedAt/DeletedAt otomatik.
- **Soft delete:** `IsDeleted` query filter; veri kaybı önlenir.
- **Calculator pattern:** Her hesaplayıcı `ICalculator<TInput, TResult>` implementasyonu, DI ile registry'ye otomatik kayıt.
- **Parametre yönetimi:** `FormulaParameter` tablosu, `ToolSlug + Key + EffectiveDate` ile zaman bazlı oran/tavan saklar. `*` ToolSlug = global parametre konvansiyonu (örn. damga vergisi, yasal faiz).
- **Caching:** Redis (resilient, in-memory fallback). FormulaParameter ve LifeTable cache'lenir.
- **Aktüeryal hesaplar:** TRH 2010 yaşam tablosu (Hazine Müsteşarlığı, 0-99 yaş erkek/kadın), `IActuarialService.AnnuityPresentValue` ile peşin değer.
- **Calculation history:** Write-only Faz 2; payload guard'lı (200KB input, 500KB output), anonim kullanıcı atlar, hata yutar (try/catch).
- **Decimal binding:** Custom `FlexibleDecimalModelBinder` virgül ve nokta ondalık ayraçlarını kabul eder; `AllowThousands` flag'i kapalı (sessiz hata önleme).

## UX

- 17/17 calculator iki kutulu pattern: lacivert "Bu hesap ne zaman kullanılır?" (info-box--info) + sarı uyarı (info-box--warning)
- 9-renkli özel palet (Fraunces, Cormorant Garamond, JetBrains Mono fontları)
- Modüler CSS: 5 katmanlı (00-foundations → 04-pages)
- Sitemap + robots.txt + per-page SEO meta

## Dokümantasyon

- `docs/legal-references.md` — 17 hesaplayıcının hukuki temelleri ve formülleri
- `docs/data-freshness.md` — Parametre güncellik tablosu
- `docs/phase-3-roadmap.md` — Faz 3 plan (admin paneli, Hangfire, bildirim sistemi)

## Önemli Dokümanlar

| Dosya | İçerik |
|---|---|
| `CLAUDE.md` | Claude Code için kalıcı bağlam ve kurallar |
| `docs/operations.md` | Operasyon runbook (cache, Hangfire, e-posta) |
| `docs/tech-debt.md` | Bilinçli ertelenen mimari kararlar |

Yeni bir mimari sapma yapıldığında `docs/tech-debt.md`'ye 4-başlıklı
format ile not düşün. Faz sonlarında bu defter gözden geçirilir.

## Kurulum

```bash
git clone <repo>
cd LexCalculus
dotnet restore
dotnet ef database update --project LexCalculus.Infrastructure --startup-project LexCalculus.Web
dotnet run --project LexCalculus.Web --launch-profile https
```

LocalDB instance: `(localdb)\MSSQLLocalDB`, db: `LexCalculusDb`.
Default admin: `admin@lexcalculus.local` (parola seed sırasında oluşturulur).

## Faz 2 sonu metrikler

| Metrik | Değer |
|---|---|
| Hesaplayıcı | 17/17 |
| Test | 219/219 |
| DB tablo | 13 |
| FormulaParameter | ~95 satır |
| Lifetable satırı | 200 (TRH 2010, 0-99 erkek/kadın) |
| Migration | 22 |
| Adım | 25/25 |

## Faz 3 — Admin Paneli + Multi-tenant + Audit (Tamamlandı: 2026-04-30)

Faz 3, sistemi tek kullanıcılı ortamdan çoklu kullanıcı + organizasyonel SaaS'a taşıdı.
Detay: `docs/phase-3-roadmap.md`.

### Adımlar

| Adım | Konu | Durum |
|------|------|-------|
| 3.1  | Admin Layout & Authorization Policy   | ✅ |
| 3.2  | FormulaParameter CRUD admin paneli    | ✅ |
| 3.3  | Veri Tazelik Bildirim Sistemi (6 parça) | ✅ |
| 3.4  | Hesap Geçmişi UI (3 parça)            | ✅ |
| 3.5  | LifeTable CRUD admin paneli           | ✅ |
| 3.6  | Kullanıcı & Rol Yönetimi (4 parça)    | ✅ |
| 3.7  | Multi-tenant altyapı (5 parça)        | ✅ |
| 3.8  | Activity Log + ExpireInvitationsJob (2 parça) | ✅ |
| 3.9  | Faz 3 cleanup + closeout (2 parça)    | ✅ |

### Faz 3 sonu metrikler

| Metrik | Değer |
|---|---|
| Test | 396/396 |
| Yeni test (Faz 3) | +70 (326 → 396) |
| Yeni migration | 6 |
| Hangfire recurring job | 2 (DataFreshnessCheck, ExpireInvitations) |
| Yeni admin sayfa | parametre, life-table, kullanıcı, tenant, talep, davet, activity-log |
| Tech-debt kapatıldı | 5 madde |
| Tech-debt açık (Faz 4'e) | 4 madde |

### Mimari kararlar (öne çıkanlar)

- **Multi-tenant:** TenantId nullable pattern; bireysel vatandaş kullanıcılar UI fark görmez
- **Plan/üyelik field YOK** — sistem ücretsiz, vatandaş odaklı vizyon
- **ShareWithTenant Core/Input DTO'larına SIZDIRILMADI** (controller seviyesi)
- **AsAdminQuery()** niyet açıklayıcı bypass extension (IgnoreQueryFilters wrapper)
- **ActivityLog:** service-level manuel log, HttpContext null'da defansif (background job uyumlu)
- **Davet kabulü:** Identity scaffolded register sayfası DEĞİŞTİRİLMEDİ, ayrı `/davet/kayit` flow

### Açık tech-debt (Faz 4)

- Madde 1: Hangfire bağımlılığı Infrastructure'a sızdı
- Madde 3: EF Core migration default uyarısı (kalıcı izleme)
- Madde 4: Hangfire 401/403 (düşük öncelik, teorik)
- Madde 9: ActivityLog retention policy (KVKK/hukuk dış bağımlılık)

## Faz 4 — Sosyal Platform (Tamamlandı: 2 Mayıs 2026)

**Başlangıç:** 30 Nisan 2026
**Bitiş:** 2 Mayıs 2026
**Tahmini süre:** ~14 hafta (3 dalga, 14 alt adım)
**Gerçek süre:** ~2 gün (Dalga A + B; Dalga C → Faz 5)
**Charter:** [docs/phase-4-charter.md](./docs/phase-4-charter.md)
**Roadmap:** [docs/phase-4-roadmap.md](./docs/phase-4-roadmap.md)

Sosyal katman: profil, bağlantı, engelleme, kullanıcı içerik üretimi
(makale + yorum + beğeni + şikayet + moderasyon).

Vizyon ilkeleri:
- Plansız (ücretsiz, herkes)
- Vatandaş 1. sınıf vatandaş
- Tenant ↔ sosyal bağımsız
- Default gizli, opt-in açık

### Dalga A — Tamamlandı (1 Mayıs 2026)

**Tag:** `phase-4-wave-a-complete` · **Süre:** ~1 gün (tahmin 3 hafta)

İçerik:
- Public profil sayfası (`/uye/{slug}`) + Person JSON-LD + sitemap
- Avatar yükleme altyapısı (MediaFile + IMediaStorage soyutlaması, ImageSharp 256x256 crop)
- UserConnections — LinkedIn modeli (Pending/Accepted/Rejected/Cancelled, Remove hard delete, 30-gün cooldown)
- `/baglantilarim` 4 sekme (Aktif / Bekleyen / Gönderdiklerim / Engellenenler)
- `/uye/{slug}/baglantilar` public bağlantı listesi (ShowConnections opt-in)
- UserBlock — sessiz pattern engelleme + cascade Accepted bağlantı silme
- Notification altyapısı genişletme (`ConnectionRequest` + `ConnectionAccepted`)
- Test: 396 → 491 (+95)
- Migration: 4 yeni (PublicProfileFields, MediaFiles, UserConnections, UserBlocks)

### Dalga B — Tamamlandı (2 Mayıs 2026)

**Tag:** `phase-4-wave-b-complete` · **Süre:** ~1 gün (tahmin 6 hafta)

İçerik:
- PostCategory + PostTag altyapısı (admin yönetimli kategoriler + kullanıcı serbest tag, popüler tag query)
- UserPost CRUD + Quill editor (rich text, featured image, tag chip, slug `/uye/{user}/makale/{post}`)
- Public makale görüntüleme + Article JSON-LD + Open Graph + sitemap entegrasyonu + disclaimer
- Görsel altyapısı: featured (1200×630 OG) + inline (1200 max) image
  AJAX upload + CSRF + ImageSharp + WebP + EXIF strip + 5 MB limit
- Yorum sistemi (sanitize + auto-link + AJAX + sahip/post sahibi/admin yetki)
- Beğeni sistemi (toggle, anonim okur görüntüler)
- Moderasyon: ContentReport + admin paneli (`/admin/sikayetler`) + Reddet/Sil
- Notification entegrasyonu: PostComment, ContentReportResolved, ContentRemoved

Test: 491 → 666 (+175 yeni, ~%36)
Migration: 4 yeni (PostCategoriesAndTags, UserPosts, PostInteractions, ContentReports)
Yeni entity: 7 (PostCategory, PostTag, UserPost, PostTagLink, PostComment, PostLike, ContentReport)
Yeni Razor Page: 4 (Makalelerim, MakaleYeni, MakaleDuzenle, Makale)
Yeni AJAX endpoint: 4 (PostImages, PostComments, PostLikes, ContentReports)
Yeni admin paneli: ContentReports moderasyon

### Faz 4 Final

**Tag:** `phase-4-complete` · **Süre:** ~2 gün (tahmin 13 hafta)

Faz 4 = Dalga A (sosyal yüzey) + Dalga B (UGC).
Dalga C (mesajlaşma 4.12-4.14) kapsam yoğunluğu nedeniyle Faz 5'e taşındı.

| Metrik | Faz başı | Faz sonu | Delta |
|---|---|---|---|
| Test | 396 | 666 | +270 (+%68) |
| Migration | 22 | 30 | +8 |
| Yeni entity | — | 9 | UserConnection, UserBlock, MediaFile, PostCategory, PostTag, UserPost, PostTagLink, PostComment, PostLike, ContentReport |
| Yeni Razor Page | — | 6 | /uye/{slug}, /uye/{slug}/baglantilar, /baglantilarim, /makalelerim, /makale-yeni, /makale-duzenle |
| Yeni public route | — | 1 | /uye/{slug}/makale/{post} |
| Yeni AJAX endpoint | — | 4 | PostImages, PostComments, PostLikes, ContentReports |
| Yeni admin paneli | — | 2 | PostCategories, ContentReports |
| Yeni NotificationType | — | 5 | ConnectionRequest, ConnectionAccepted, PostComment, ContentReportResolved, ContentRemoved |

### Açık tech-debt (Faz 5'e devredildi)

Faz 4 boyunca biriken 13 yeni madde (`docs/tech-debt.md` 11-23):
InMemory test sınırları, media GC, Hide vs Delete moderation, hierarchical
reply, tag autocomplete, view count dedupe, tag helper extract, image
responsive variants, hesap silme/anonimize, bot/spam rate limiting,
comment edit history, notification email kanalı, NoIndex bayraktarması.

Önceki açık (Faz 3'ten devren): Madde 1, 3, 4, 9 — durumları değişmedi.

---

## Faz 5 Dalga B — Tamamlandı (5 Mayıs 2026)

**Tag:** `phase-5-wave-b-complete` · **Süre:** ~2 gün (tahmin 3 hafta)

1-1 doğrudan mesajlaşma altyapısı tam yayında. Conversation + Message
entity, /mesajlar UI, polling fallback, SignalR real-time, mesaj
moderasyonu (Hide/Sil + Şikayet), engelleme + anonimize entegrasyonu.

| Adım | Konu | Sonuç |
|---|---|---|
| 5.4 | Conversation + Message entity + servis | Yetki: bağlantı OR aynı tenant + NOT engelleme; deterministic User1Id<User2Id |
| 5.5 | /mesajlar UI + AJAX endpoint + polling | 3 sayfa (Index/Detail/Yeni), 5 endpoint, 30 sn polling, kebab Sil |
| 5.6 | SignalR real-time | IMessagingNotifier abstraction, Hub /hubs/messages, polling fallback korunur |
| 5.7 | Mesaj moderasyon + closeout | Şikayet butonu, IsModeratorHidden, admin Hide/Unhide/Sil, real-time MessageHidden event |

| Metrik | Faz 4 sonu | Dalga B sonu | Delta |
|---|---|---|---|
| Test | 666 | 779 | +113 (+%17) |
| Migration | 30 | 32 | +2 (AddMessaging, AddMessageModeratorHide) |
| Yeni entity | — | 2 | Conversation, Message |
| Yeni Razor Page | — | 3 | /mesajlar, /mesajlar/{id}, /mesajlar/yeni |
| Yeni AJAX endpoint | — | 5 | send, delete, get, new (polling), mark-read |
| Yeni teknoloji | — | SignalR + WebSocket + IHubContext | — |
| Genişletilen enum | — | ContentReportTargetType.Message=3 | — |

## Faz 5 — Tamamlandı (15 Mayıs 2026)

**Tag:** `phase-5-complete` (manuel doğrulama sonrası) · **Süre:** 2 Mayıs → 15 Mayıs 2026 (~2 hafta, charter 6 hafta tahmini)

Faz 5 = Dalga A + B + C (9 alt adım). 1-1 mesajlaşma, KVKK uyumlu hesap silme,
spam koruması (rate limiting), hide moderation, ve tüm test altyapısının InMemory'den
SQL Server LocalDB'ye geçişi.

### Dalga A — Olgunlaştırma (3 Mayıs 2026)
- **5.1** KVKK hesap silme + anonimize (UserAnonymizationService, hard delete YOK)
- **5.2** Rate limiting middleware (5 named policy: comment/report/message/connection/ajax-general)
- **5.3** Hide moderation (UserPost/Comment `IsModeratorHidden`) + Authorize endpoint'lerine otomatik NoIndex

### Dalga B — Mesajlaşma altyapısı (5 Mayıs 2026)
- **5.4** Conversation + Message entity + servis (CanMessage: bağlantı OR aynı tenant + NOT engelleme; deterministic User1Id<User2Id)
- **5.5** `/mesajlar` UI + 5 AJAX endpoint + 30 sn polling fallback
- **5.6** SignalR real-time (`IMessagingNotifier` abstraction, Hub `/hubs/messages`, polling fallback korunur)
- **5.7** Mesaj moderasyon (kebab Şikayet + admin Hide/Unhide/Sil + real-time MessageHidden event)

### Dalga C — Test infrastructure + closeout (15 Mayıs 2026)
- **5.8** SQL Server LocalDB test migration (P1 pilot + P2 tam geçiş; InMemory provider kaldırıldı)
- **5.9** Faz 5 closeout (bu commit)

### Metrikler

| Metrik | Faz 4 sonu | Faz 5 sonu | Delta |
|---|---|---|---|
| Test | 666 | 779 | +113 (+%17) |
| Migration | 30 | 32+ | +2 (AddMessaging, AddMessageModeratorHide) |
| Yeni entity | — | 2 | Conversation, Message |
| Yeni Razor Page | — | 3 | `/mesajlar`, `/mesajlar/{id}`, `/mesajlar/yeni` |
| Yeni AJAX endpoint | — | 5 | send, delete, get, new (polling), mark-read |
| Yeni teknoloji | — | SignalR + WebSocket + IHubContext | — |
| Yeni rate limit policy | — | 5 named | comment/report/message/connection/ajax-general |
| Test altyapısı | InMemory | SQL Server LocalDB | per-test fresh DB, production-yakın semantik |

### Lex Calculus artık
- Hukuki hesaplamalar (Faz 1-3): 17/17 hesaplayıcı aktif
- Kullanıcı kayıt + tenant (hukuk büroları), multi-tenant izolasyon
- Public profil + LinkedIn-style bağlantı + engelleme + notification
- Makale + Quill editor + featured/inline image + tag/kategori
- Public makale + SEO + sitemap
- Yorum + beğeni + AJAX
- Moderasyon: şikayet + admin Hide/Sil paneli (Post + Comment + Message)
- **Real-time mesajlaşma** (SignalR, polling fallback)
- **KVKK uyumlu** hesap silme (anonimize stratejisi, hard delete YOK)
- **Spam koruması** (rate limiting middleware, 5 named policy)
- **Test infrastructure:** SQL Server LocalDB (production-yakın IDENTITY/FK/transaction semantiği)

### Faz 6 — Olgunlaştırma + UX + Performance (başladı 29 Mayıs 2026)

3 dalga, 12 alt adım (6.2-6.13). Email notification kanalı, UX iyileştirmeleri,
performance + cleanliness.

- [Faz 6 Charter](./docs/phase-6-charter.md) · [Roadmap](./docs/phase-6-roadmap.md) · [Kapsam envanteri](./docs/phase-6-scope-inventory.md)

#### Faz 6 Dalga B — Tamamlandı (1 Haziran 2026)

**Tag:** `phase-6-wave-b-complete` · **Süre:** 30 Mayıs-1 Haziran 2026 (charter 1-1.5 hafta tahmini)

UX iyileştirmeleri (Dalga A pattern reuse — server-side endpoint + vanilla JS + XSS-safe):
- Tag autocomplete (`/api/post-tags/search` prefix endpoint + Quill vanilla JS dropdown)
- View count dedupe (anonim cookie 30 dk TTL + login `IMemoryCache` 30 dk sliding)
- Polling Page Visibility API (sekme gizliyse pause, visible → hemen poll + interval)
- Mesaj sayfalama (`/older` endpoint + "Daha fazla yükle" + scroll pozisyon koruması)
- Multi-tab read-state foundation (`IMessagingNotifier` 4. method + `ConversationRead`
  SignalR broadcast — alıcının tüm tab'larına)
- Comment edit history (`PostCommentRevision` ilk-orijinal saklama + `(orijinali göster)`
  toggle + lazy fetch + anon `/original` endpoint)
- Image responsive variants (SixLabors.ImageSharp 480w + 800w WebP + render-time
  `ImageVariantEnricher` srcset/sizes/lazy)

**Süreç notu (şeffaflık):** Adım 6.7'de #40 polling fallback tarayıcı smoke'u denendi
ancak "Network Offline" yanlış senaryoydu — tüm HTTP kesilir, polling de ölür, dolayısıyla
"WS kopunca polling'e düşüyor mu" test edilemez. Doğru senaryo WS bloke + HTTP açık olmalı;
bütünsel manuel smoke Adım 6.13 Faz 6 closeout'a taşındı (tech-debt #40).

| Metrik | Değer |
|---|---|
| Test | 798 → 819 (+21, regresyon 0) |
| Migration | +1 (AddPostCommentRevisions) |
| Yeni entity | PostCommentRevision |
| Build uyarı | 0 hata + 0 uyarı (Dalga A'dan korundu, yeni NU1901 yok) |
| Kapatılan tech-debt | #15, #16, #18, #21, #24, #25 (6 madde) |
| Kısmen / bekleyen | 🟡 #37 (backend hazır, liste real-time Faz 7+) · 🟡 #40 (→ 6.13) |

Sıradaki: **Dalga C — Performance + closeout (6.10-6.13)**.

#### Faz 6 Dalga A — Tamamlandı (30 Mayıs 2026)

**Tag:** `phase-6-wave-a-complete` · **Süre:** 29-30 Mayıs 2026 (charter 1.5-2 hafta tahmini)

Email + temizlik:
- Sosyal bildirim email template'leri (Connection, Comment, ContentReport, MessageDigest)
  — Faz 3 email altyapısı (IEmailService, EmailTemplateRenderer, 3 provider) reuse
- `INotificationEmailDispatcher` — master switch + 4 granüler tercih + anonimize gating
- `ProcessMessageDigestJob` (Hangfire, 5 dk eşik + user-level all-pending grup)
- `/profil` email tercih UI (master + 4 granüler toggle, master kapalıyken pasif)
- NU1901 NuGet açığı (transitive pinning, NuGet.Packaging/Protocol 6.12.5)
- CA2024 async EndOfStream temizliği (LifeTableCsvParser)

**Süreç notu (şeffaflık):** Adım 6.2 P2'de `NotificationsEmailEnabled` (#39)
"orphan" sanılırken `DataFreshnessCheckJob` + `/profil` tarafından aktif
kullanıldığı tespit edildi. Planlanan drop iptal edildi; alan "tüm e-postalar"
ana anahtarı (master) olarak korundu, 4 granüler tercih altına eklendi. Envanter
denetim süreç borcu tech-debt #41 olarak kaydedildi.

| Metrik | Değer |
|---|---|
| Test | 779 → 798 (+19, regresyon 0) |
| Migration | +1 (AddSocialEmailPreferencesAndDigest) |
| Yeni entity / servis / job | EmailDigestEntry / INotificationEmailDispatcher / ProcessMessageDigestJob |
| Build uyarı | NU1901 + CA2024 → 0 |
| Kapatılan tech-debt | #22 (email kanalı), #35 (NU1901), #36 (CA2024) |

Sıradaki: **Dalga B — UX iyileştirmeler (6.6-6.9)**.

---

LexCalculus, Türk hukukunun karmaşık hesaplamalarını şeffaf ve denetlenebilir kılmak için tasarlandı.

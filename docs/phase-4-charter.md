# Lex Calculus — Faz 4 Charter

**Faz:** 4 — Sosyal Platform (Profil + Bağlantı + UGC + Mesajlaşma)
**Başlangıç:** 30 Nisan 2026
**Tahmini süre:** ~14 hafta (3 dalga, 14 alt adım)
**Tag (kapanış):** `phase-4-complete`
**Charter sürümü:** 1.0

---

## 1. Vizyon ve Bağlam

### 1.1 Faz 4'ün Yeri

Faz 3'te (admin paneli + multi-tenant + audit) Lex Calculus production-ready bir SaaS platformuna dönüştü. Faz 4'ün amacı, platforma **sosyal katmanı** eklemektir: kullanıcılar arasında bağlantı kurma, içerik üretme/tüketme, mesajlaşma.

Faz 4, orijinal teknik dokümanın 7. bölümünü (Sosyal Platform Özellikleri) baz alır ancak Faz 3'teki vizyon kararlarına uygun olarak revize edilmiştir. En kritik fark: doküman avukat-only profesyonel ağ varsayarken, Faz 4 **vatandaş + avukat birlikte** yaklaşımını benimser (Faz 3'teki "tüm vatandaşlar" vizyonu).

### 1.2 Vizyon İlkeleri

Faz 3'ten devralınan ve Faz 4'te de geçerli olan ilkeler:

- **Sistem ücretsizdir.** Plan/tier kavramı yoktur (Free/Pro/Premium yok).
- **Vatandaş 1. sınıf vatandaştır.** Sosyal platform da plansız; vatandaş profil oluşturabilir, bağlantı kurabilir, içerik yazabilir.
- **Kullanıcı kontrolü maksimum.** Default'lar gizli/kapalı, kullanıcı bilinçli olarak açar.
- **Tenant katmanı bağımsız.** Sosyal katman tenant'tan ayrı yaşar; tenant üyeliği otomatik sosyal bağlantı doğurmaz.
- **Defense-in-depth.** Görünürlük kararları DB seviyesinde (query filter veya alan kontrolü) korunur.

### 1.3 Faz 4 Kapsamı (Net)

**KAPSAMDA:**
- Public profile sayfası (opt-in, ayrı tenant görünürlük toggle'ı ile)
- Avatar yükleme + Bio + City alanlarının UI'a bağlanması
- UserConnections (LinkedIn modeli — bağlantı isteği akışı)
- UserBlock (engelleme sistemi)
- UGC: makale/blog (kullanıcı içerik üretimi)
- Yorum + Beğeni + Şikayet + Moderasyon
- Görsel yükleme (yerel disk başlangıç)
- Mesajlaşma (SignalR + Conversations + Messages)
- Notification altyapısının genişletilmesi (Connection, Comment, Like, CommentReply, Reported, Message tipleri)

**KAPSAM DIŞI (Faz 5+'e):**
- OAuth providers (Google, Microsoft) — sade cookie auth yetiyor
- JWT — gerek yok (sadece web)
- Admin CMS (Pages, sistem-yazılı blog) — UGC bunu kapsamıyor
- Dış entegrasyonlar (TCMB, Resmi Gazete, Yargıtay scraper'ları)
- Calculator kategorileri D-I (15 yeni hesaplayıcı)
- ActivityLog CSV export, retention policy
- Versiyon kontrolü (makale revizyon geçmişi — yayın sonrası serbest düzenleme yeterli)
- Azure Blob storage (yerel disk yeterli, sonra geçilir)
- Hreflang / multi-locale
- Elasticsearch full-text search (DB LIKE yeterli, sonra Elasticsearch)

### 1.4 Faz 4 Sonrası Beklenen Durum

Faz 4 kapandığında platform şunu yapabilir olacak:
- Vatandaş veya avukat kayıt olur, profil bilgilerini doldurur
- Profilini kamuya açabilir (opsiyonel olarak büro bilgisini de gösterir)
- Diğer kullanıcıları bulabilir, bağlantı isteği gönderir
- Rahatsız eden kullanıcıları engelleyebilir
- Hukuki/genel makale yazar, görsel ekler, yayınlar
- Diğerlerinin makalelerini okur, yorumlar, beğenir
- Uygunsuz içeriği bildirir; admin müdahale eder
- Bağlı olduklarıyla mesajlaşır (real-time)
- Tüm bu olaylar için bildirim alır

---

## 2. Mimari Kararlar

### 2.1 Kullanıcı Erişim Kararları

| # | Karar | Seçim | Gerekçe |
|---|---|---|---|
| 1 | Public profile toggle | İki ayrı: profil açık/gizli + tenant görünürlüğü açık/gizli | Kullanıcı profil kamuya açıkken büro bilgisini gizleyebilmeli |
| 2 | Bağlantı isteği şartı | LinkedIn modeli — herkese gönderilebilir, alıcı kabul/red | Vatandaş × avukat etkileşim alanı |
| 3 | Tenant ↔ sosyal ilişkisi | Bağımsız — aynı tenantta olmak otomatik bağlı yapmaz | Profesyonel ilişki ≠ kişisel sosyal bağ |
| 4 | Vatandaş kapsamı | 1. sınıf vatandaş — profil + bağlantı + içerik üretimi yapabilir | Plansız vizyon |
| 5 | UserBlock var | Engellenen mesaj atamaz, profili göremez | LinkedIn modeli, kullanıcı kontrolü |

### 2.2 İçerik Üretimi (UGC) Kararları

| # | Karar | Seçim | Gerekçe |
|---|---|---|---|
| 6 | İki katmanlı moderasyon | Sahip günlük (yorum sil), Admin sistem geneli (içerik kaldır, kullanıcı askıya al) | Hukuki sorumluluk + KVKK |
| 7 | Şikayet sistemi | "Bildir" butonu → admin onay sırası | Topluluk öz-denetimi + admin son söz |
| 8 | Disclaimer | Her makaleye otomatik footer: "kişisel görüş, hukuki tavsiye değil" | Yayın hukuku, hukuki güvence |
| 9 | Taslak/Yayında | `IsPublished` + `PublishedAt nullable`. Taslak sadece sahip görür | Doğal yazım akışı |
| 10 | Düzenleme | Yayın sonrası serbest, "son güncellenme: X" gösterilir | Basitlik — versiyon kontrolü Faz 5+ |
| 11 | Slug stratejisi | `/uye/{user-slug}/makale/{post-slug}` — kullanıcı namespace altında | Çakışma önleme + sahiplik açık |
| 12 | Tag stratejisi | Karma — popüler tag'ler önerilir, yenisi de eklenebilir | Esnek + dağılımı kontrol altında |
| 13 | Görsel altyapı | Yerel disk (`wwwroot/uploads/posts/`), 5 MB max, JPG/PNG/WebP, 10 görsel/post | Basit başlangıç, Azure Blob sonra |
| 14 | Avatar | Aynı görsel altyapısı (UserProfile.AvatarUrl entity'de zaten var) | DRY |

### 2.3 Mesajlaşma Kararları

| # | Karar | Seçim | Gerekçe |
|---|---|---|---|
| 15 | Real-time | SignalR | Doküman'ın orijinal teknolojisi |
| 16 | Mesajlaşma şartı | Engelleme dışında serbest (bağlantı şartı YOK) | LinkedIn'de de bağlantısıza mesaj atılabilir |
| 17 | Engelleme entegrasyon | Engellenen kullanıcı mesaj atamaz, mevcut konuşma görünmez | Karar 5 ile tutarlı |
| 18 | Mesaj retention | Kalıcı (silme YOK) | Faz 4 minimum kapsam; retention Faz 5'e |

### 2.4 Genel Pattern Kararları

| # | Karar | Seçim | Gerekçe |
|---|---|---|---|
| 19 | Notification altyapısı | Mevcut (Faz 3.3) genişletilir, yeniden yazılmaz | NotificationType enum'a yeni değerler eklenir |
| 20 | Bağlantı state machine | TenantInvitation pattern reuse (Pending/Accepted/Cancelled/Rejected) | Faz 3.7'de oturmuş pattern |
| 21 | Audit log | UGC ve sosyal eylemler ActivityLog'a yazılır (genişletme) | Faz 3.8 pattern'i devam |
| 22 | Vatandaş UI gürültüsüzlüğü | Profil gizli iken tüm UI'da gizli kalır | Faz 3'teki TenantId nullable pattern devamı |

---

## 3. Mimari Genel Bakış

### 3.1 Yeni Katmanlar

Faz 4'te yeni katman eklenmez. Mevcut katmanlar (Core / Infrastructure / Web / Tests / Jobs) içinde yeni dosyalar.

**SignalR istisnası:** SignalR Hub'ları teknik olarak Web katmanında yaşar (`LexCalculus.Web/Hubs/`), ancak ConversationService Core/Infrastructure'da kalır.

### 3.2 Yeni Klasör Pattern'leri

| Konum | İçerik |
|---|---|
| `LexCalculus.Core/Entities/Social/` | UserConnection, UserBlock |
| `LexCalculus.Core/Entities/Content/` | UserPost, PostCategory, PostTag, PostTagLink, PostComment, PostLike, MediaFile, ContentReport |
| `LexCalculus.Core/Entities/Messaging/` | Conversation, Message |
| `LexCalculus.Core/Services/` | I*-arayüzler (Connection, Block, UserPost, PostComment, PostLike, MediaUpload, ContentReport, Conversation) |
| `LexCalculus.Infrastructure/Services/` | Servis implementasyonları |
| `LexCalculus.Infrastructure/Storage/` | LocalDiskMediaStorage (gelecekte AzureBlobMediaStorage) |
| `LexCalculus.Web/Controllers/` | UyeController (public profile), BaglantilarController, MakalelerimController, MakaleController, MesajlarController |
| `LexCalculus.Web/Areas/Admin/Controllers/` | PostCategoriesController, ContentReportsController |
| `LexCalculus.Web/Hubs/` | MessagingHub |
| `LexCalculus.Web/ViewComponents/` | ConnectionRequestBadgeViewComponent, NewMessagesBadgeViewComponent |

### 3.3 Yeni Entity'ler (Özet)

```
Social:
  UserConnection (RequesterId, TargetId, Status, CreatedAt, RespondedAt)
  UserBlock (BlockerId, BlockedId, CreatedAt)

Content:
  PostCategory (Id, Name, Slug, IsActive)
  PostTag (Id, Name, Slug, UsageCount)
  UserPost (Id, UserId, CategoryId, Slug, Title, Body, FeaturedImageUrl,
            IsPublished, PublishedAt, ViewCount, CreatedAt, UpdatedAt)
  PostTagLink (PostId, TagId)
  PostComment (Id, PostId, UserId, ParentCommentId, Body, CreatedAt, IsDeleted)
  PostLike (PostId, UserId, CreatedAt)
  MediaFile (Id, UserId, FileName, OriginalName, RelativePath, MimeType, SizeBytes, CreatedAt)
  ContentReport (Id, ReporterId, TargetType, TargetId, Reason, Status, CreatedAt, ResolvedAt, ResolvedByUserId, AdminNote)

Messaging:
  Conversation (Id, ParticipantAUserId, ParticipantBUserId, LastMessageAt, CreatedAt)
  Message (Id, ConversationId, SenderId, Body, IsRead, ReadAt, CreatedAt)
```

### 3.4 Yeni Notification Tipleri

`NotificationType` enum'a eklenen değerler (mevcut: DataFreshness, ParameterChange, SystemAlert):

- `Connection` (yeni bağlantı isteği geldi)
- `ConnectionAccepted` (bağlantı isteğin kabul edildi)
- `Comment` (makaleni yorumlandı)
- `CommentReply` (yorumun yanıtlandı)
- `Like` (makalen beğenildi)
- `Reported` (makalen şikayet edildi — sahibe bilgi)
- `ContentRemoved` (içeriğin admin tarafından kaldırıldı)
- `Message` (yeni mesaj geldi)

### 3.5 Yeni ActivityLog Action'ları

`{Entity}.{Verb}` pattern'i devam:

- `UserConnection.Send`, `.Accept`, `.Reject`, `.Cancel`
- `UserBlock.Create`, `.Remove`
- `UserPost.Create`, `.Publish`, `.Unpublish`, `.Update`, `.Delete`, `.AdminRemove`
- `PostComment.Create`, `.Delete` (sahip), `.AdminDelete`
- `PostLike.Toggle`
- `ContentReport.Create`, `.Resolve`, `.Dismiss`
- `Conversation.Create`
- `Message.Send`

### 3.6 Migration Tahmini

Tahmini 8-10 yeni migration:

1. `AddPublicProfileFields` — UserProfile zaten alanları içerir (IsPublicProfile, Bio, AvatarUrl, City), eksik varsa ek alanlar (ShowTenant, PublicSlug) — kontrol gerekli, **belki migration yok**
2. `AddUserConnections`
3. `AddUserBlocks`
4. `AddPostCategories`
5. `AddPostTags`
6. `AddUserPosts` (UserPost + PostTagLink)
7. `AddMediaFiles`
8. `AddPostComments` + `AddPostLikes` (tek migration olabilir)
9. `AddContentReports`
10. `AddMessaging` (Conversation + Message)

---

## 4. Dalga Yapısı

Faz 4 üç dalgaya bölünmüştür. Sıralama bağımlılığı:

```
Dalga A (Profil + Bağlantı + Engelleme)
        ↓
Dalga B (UGC: kategoriler + makaleler + yorum/beğeni + şikayet)
        ↓
Dalga C (Mesajlaşma: SignalR + Conversation + Messages)
```

**Bağımlılık gerekçesi:**
- B, A'nın profil sayfasını kullanır (yazar profili, makale altında "Bağlantı İste")
- B, A'nın engelleme sistemini kullanır (engelli kullanıcı yorum yazamaz)
- C, A'nın engelleme sistemini kullanır (engelli mesaj atamaz)
- C, B'den bağımsız ama B sonrası başlamak doğru çünkü mesajlaşma sayfası "yazardan mesaj at" gibi entegrasyonlar B'deki UGC ile birlikte düşünülecek

### 4.1 Dalga A — Profil + Bağlantı + Engelleme + Notification (4 alt adım, ~3 hafta)

**Adım 4.1:** Public profile UI binding
**Adım 4.2:** UserConnections altyapısı
**Adım 4.3:** UserBlock + Notification genişletme
**Adım 4.4:** Dalga A closeout (manuel doğrulama, commit, mini tag)

### 4.2 Dalga B — UGC (7 alt adım, ~6 hafta)

**Adım 4.5:** PostCategory + PostTag altyapısı
**Adım 4.6:** UserPost entity + temel CRUD + editör
**Adım 4.7:** Public makale görüntüleme + SEO
**Adım 4.8:** Görsel yükleme altyapısı
**Adım 4.9:** PostComment + PostLike
**Adım 4.10:** Şikayet sistemi + admin moderasyon
**Adım 4.11:** Dalga B closeout

### 4.3 Dalga C — Mesajlaşma (3 alt adım, ~3 hafta)

**Adım 4.12:** SignalR altyapı + Conversation/Message entity'leri
**Adım 4.13:** Mesajlaşma UI (real-time)
**Adım 4.14:** Faz 4 closeout (Notification.Message + bell + README + roadmap + tag)

**Toplam: 14 alt adım.**

Her adım Faz 3'teki gibi parçalara bölünebilir (örn. Adım 4.6 = P1 entity + P2 CRUD + P3 editör). Bu kararı her adım başlangıcında veririz.

---

## 5. Risk ve Tuzaklar

### 5.1 Faz 3'ten Bilinen Tuzaklar (Devam Eder)

- **EF Core query filter expression** — soft-delete + visibility filter birleştirme
- **Migration default value** — yeni nullable olmayan kolonlar için default kontrolü
- **CSS scope ayrımı** — `tenant-yonetim__*` pattern'i UGC için de geçerli (`makale__*`, `yorum__*`)
- **SiteUrl konfigürasyondan** — sosyal paylaşım URL'leri SeoSettings.SiteUrl kullanır

### 5.2 Faz 4'e Özgü Yeni Tuzaklar

| Tuzak | Etkilenen alan | Hafifletme |
|---|---|---|
| **SignalR + scale-out** | Birden fazla web instance varsa SignalR Redis backplane gerekir | Faz 4'te tek instance varsayımı; Redis backplane Faz 5+ |
| **Görsel yükleme güvenliği** | MIME spoofing, virüs, EXIF metadata | Strict whitelist (JPG/PNG/WebP), MIME re-validation, EXIF strip |
| **N+1 query (sosyal feed)** | Bağlantıların makaleleri sayfalanırken her makale için yazar, yorum sayısı, beğeni sayısı | Projection + Include + DTO mapping |
| **Slug çakışması** | İki kullanıcı aynı başlık → aynı slug | User namespace altında çözülüyor (Karar 11) |
| **View count race** | Aynı zamanda iki istek view++ → kayıp | `ExecuteUpdate` ile atomik increment |
| **Şikayet spam'i** | Bir kullanıcı bir içeriğe defalarca şikayet | Şikayet başına unique constraint (ReporterId + TargetType + TargetId) |
| **Engellemenin geriye dönük etkisi** | A B'yi engellerse mevcut konuşmaları/yorumları nasıl ele alınır? | Konuşmalar gizlenir (her iki yana), yorumlar kalır ama engelleyen göremez |
| **Public profile gizliyken sitemap** | Profil gizli olunca sitemap'te kalmamalı | Sitemap query: `IsPublicProfile == true` filter |
| **Real-time UI senkronizasyon** | Mesaj geldi ama bell güncellenmedi | SignalR client → bell ViewComponent refresh event |
| **Engelleme + mesajlaşma race** | Mesaj atılırken karşı taraf engelliyor | Server-side son kontrol, atılan mesaj reject edilir |

### 5.3 Hukuki ve Etik Riskler

| Risk | Konu | Hafifletme |
|---|---|---|
| **Yanlış hukuki bilgi** | Vatandaş yanlış makale yazar, başka vatandaş zarara uğrar | Otomatik disclaimer footer (Karar 8) |
| **Hakaret/iftira** | Makale veya yorum üzerinden | Şikayet sistemi + admin müdahale (Karar 6, 7) |
| **KVKK kişisel veri** | Public profilde fazla bilgi | Default gizli, opt-in açık (Karar 1) |
| **Mesajlaşma içeriği** | Hassas içerik (avukat-müvekkil ayrıcalığı?) | İlk versiyon: kalıcı saklama, retention Faz 5 — kullanıcı bilgilendirme |
| **Telif/copyright** | Kullanıcı yüklediği görsel için sorumluluk | Ek tech-debt: Terms of Service kabulü Faz 5+ |

---

## 6. Test Stratejisi

### 6.1 Faz 4 Test Hedefi

Faz 3 sonu: 396 test. Faz 4 hedef: **~600+ test** (Dalga A: +50, Dalga B: +100, Dalga C: +50).

### 6.2 Yeni Test Pattern'leri

- **VisibilityRulesTests** — public profile + tenant görünürlük + engelleme kombinasyonları
- **ConnectionFlowTests** — bağlantı state machine (TenantInvitation pattern reuse)
- **PostPublishTests** — taslak ↔ yayında geçişleri
- **ModerationTests** — sahip vs admin yetkisi sınırları
- **ReportFlowTests** — şikayet → admin onay → resolve/dismiss
- **MessagingTests** — engelleme + mesaj atma reddi, conversation oluşturma kuralları

### 6.3 Manuel Test Yorgunluğu

Faz 3'te P3/5 ve P4/5 sonrası manuel test yorucuydu. Faz 4'te:
- Her adım sonrası belirli senaryolar listesi (komutta yer alır)
- Tarayıcıda manuel doğrulama her zaman beklenir
- Eylem hızı yavaşlarsa Claude'un komutu daha küçük parçalara bölmesi tercih edilir

---

## 7. Açık Tech-debt (Faz 4 Sırasında Eklenebilir)

Mevcut açık (Faz 3'ten):
- **Madde 1:** Hangfire bağımlılığı Infrastructure'a sızdı (Faz 4 başlangıcında refactor öncelik)
- **Madde 3:** EF Core migration default değer (kalıcı izleme)
- **Madde 4:** Hangfire 401/403 yetki ayırma (düşük öncelik)
- **Madde 9:** ActivityLog retention policy (KVKK avukat görüşü)

Faz 4'te yeni tech-debt madde adayları:
- SignalR Redis backplane (multi-instance scale)
- Azure Blob storage geçişi (yerel diskten)
- Mesaj retention policy (KVKK)
- Görsel CDN (yerel servis)
- Elasticsearch full-text search (DB LIKE yetersiz olunca)
- Hreflang (TR/EN)

---

## 8. Faz 4 Tamamlanma Kriterleri

Aşağıdakilerin tümü ✓ olduğunda Faz 4 kapanır:

1. 14 alt adım tümü tamamlandı
2. Test sayısı 600+ (regresyon: 0)
3. Tüm dalgalar manuel doğrulamadan geçti
4. README'de Faz 4 closeout bölümü
5. `docs/phase-4-roadmap.md` tüm adımlar ✅
6. `phase-4-complete` annotated tag
7. `docs/tech-debt.md` güncellendi (Faz 4 maddeleri eklendi)

---

## 9. Faz 5 Önizleme (Bilgi Amaçlı)

Faz 4'ün doğal devamı olabilecek konular:

- **Admin CMS** (Pages, sistem-yazılı blog, Kategori master yönetimi)
- **Dış entegrasyonlar** (TCMB, Resmi Gazete, Yargıtay scraper'ları)
- **Calculator kategorileri D-I** (15 yeni hesaplayıcı: Vergi, Aile, Miras, vs.)
- **OAuth providers** (Google, Microsoft)
- **Azure Blob storage** geçişi
- **Elasticsearch** full-text search
- **Hreflang / multi-locale** (TR/EN)
- **404 + Redirect modülü**
- **ActivityLog CSV export + retention**
- **Mesaj retention policy** (KVKK)
- **Versiyon kontrolü** (makale revizyonu)

Bunların hiçbiri Faz 4 kapsamında değildir.

---

*Charter sürümü 1.0 — 30 Nisan 2026. Faz 4 boyunca güncel kalır; charter değişiklikleri commit'lerle işlenir.*

---

## 10. Implementation Status (Kapanış Notu — 2 Mayıs 2026)

Faz 4 = Dalga A + Dalga B (10 alt adım, 4.1-4.10 + 4.11 closeout) tamamlandı.
Dalga C (mesajlaşma, 4.12-4.14) kapsam yoğunluğu nedeniyle Faz 5'e taşındı.

### Charter Karar Implementation Sonuçları

**Kullanıcı Erişim Kararları (§2.1):**

| # | Karar | Durum | Adım |
|---|---|---|---|
| 1 | İki ayrı toggle (profil + tenant görünürlüğü) | ✅ | 4.1 P1 |
| 2 | LinkedIn modeli bağlantı | ✅ | 4.2 P1+P2+P3a+P3b |
| 3 | Tenant ↔ sosyal bağımsız | ✅ | 4.1, 4.2 boyunca |
| 4 | Vatandaş 1. sınıf | ✅ | tüm Faz 4 |
| 5 | UserBlock var | ✅ | 4.3 |

**İçerik Üretimi (UGC) Kararları (§2.2):**

| # | Karar | Durum | Adım |
|---|---|---|---|
| 6 | İki katmanlı moderasyon | ✅ | 4.9 (sahip), 4.10 (admin) |
| 7 | Şikayet + admin onay sırası | ✅ | 4.10 P1+P2 |
| 8 | Otomatik disclaimer footer | ✅ | 4.7 |
| 9 | Taslak/Yayında (IsPublished) | ✅ | 4.6 P1+P2 |
| 10 | Yayın sonrası serbest düzenleme | ✅ | 4.6 P3 |
| 11 | Slug `/uye/{user}/makale/{post}` | ✅ | 4.6 P1, 4.7 |
| 12 | Karma tag stratejisi | ✅ | 4.5, 4.6 P3 |
| 13 | Görsel: yerel disk + 5 MB + JPG/PNG/WebP | ✅ | 4.8 |
| 14 | Avatar = aynı görsel altyapısı | ✅ | 4.1 P3 + 4.8 |

**Mesajlaşma Kararları (§2.3):** ↪️ Faz 5 (15-18 arası kararlar Faz 5'e devredildi)

**Genel Pattern Kararları (§2.4):**

| # | Karar | Durum | Adım |
|---|---|---|---|
| 19 | Notification altyapısı genişletildi (yeniden yazılmadı) | ✅ | 4.3, 4.9, 4.10 |
| 20 | Bağlantı state machine TenantInvitation pattern reuse | ✅ | 4.2 P1 |
| 21 | UGC + sosyal eylemler ActivityLog'a yazılır | ✅ | 4.5-4.10 boyunca |
| 22 | Vatandaş UI gürültüsüzlüğü (gizli iken her yerde gizli) | ✅ | 4.1 + 4.7 (like sessiz) |

### Tamamlanma Kriterleri (§8)

| # | Kriter | Durum |
|---|---|---|
| 1 | 14 alt adım tümü tamamlandı | ⚠️ Kısmen — 4.1-4.11 ✅ (10 adım), 4.12-4.14 → Faz 5 |
| 2 | Test sayısı 600+ (regresyon: 0) | ✅ 666 (hedef 600+ aşıldı) |
| 3 | Tüm dalgalar manuel doğrulamadan geçti | ✅ Dalga A + B (Dalga C → Faz 5) |
| 4 | README'de Faz 4 closeout bölümü | ✅ |
| 5 | `docs/phase-4-roadmap.md` adımlar ✅/↪️ | ✅ |
| 6 | `phase-4-complete` annotated tag | ✅ Adım 4.11'de |
| 7 | `docs/tech-debt.md` Faz 4 maddeleri | ✅ Madde 11-23 (13 yeni) |

**Karar:** Faz 4 kapanışı **scope-revize** sayılır — orijinal 14 adımdan
10'u tamamlandı, 4'ü (mesajlaşma) Faz 5'e taşındı. Sosyal platform + UGC
yayında çalışıyor; mesajlaşma bağımsız bir kapsam, ertelemenin teknik
maliyeti yok.

### Faz 5'e Devredilen Kararlar

- Karar 15-18 (Mesajlaşma, §2.3): SignalR, mesaj retention, engelleme
  entegrasyonu — Faz 5 başlığı altında ele alınacak
- Faz 4 Dalga B'de biriken 13 tech-debt maddesi (`docs/tech-debt.md`
  madde 11-23): Faz 5'te öncelikleri değerlendirilir

### Süre Karşılaştırması

| Kapsam | Tahmin | Gerçek | Oran |
|---|---|---|---|
| Dalga A (4 adım, ~3 hafta) | 3 hafta | ~1 gün | ~21x |
| Dalga B (7 adım, ~6 hafta) | 6 hafta | ~1 gün | ~42x |
| Faz 4 (Dalga A + B, 10 adım) | ~9 hafta | ~2 gün | ~32x |

Bu oran Faz 5 planlamasında baseline değildir — Faz 4'ün hızı
LLM-asistasyonlu development'ın boyut tahminini sarsıcı şekilde
küçülttüğünü göstermekle birlikte, Faz 5 (real-time, KVKK) farklı
karmaşıklık profili taşıdığından yeni baseline'a ihtiyaç var.

---

*Implementation Status notu — 2 Mayıs 2026, Adım 4.11 closeout commit'i.*

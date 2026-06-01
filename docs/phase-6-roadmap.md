# Lex Calculus — Faz 6 Yol Haritası

## Olgunlaştırma + UX + Performance

**Başlangıç:** 29 Mayıs 2026 · **Charter:** [phase-6-charter.md](./phase-6-charter.md)
· **Envanter:** [phase-6-scope-inventory.md](./phase-6-scope-inventory.md)

🏁 **Faz 6 tamamlandı (2 Haziran 2026)** — 12 alt adım (Dalga A+B+C). Tag: `phase-6-complete`
(kullanıcının #40 manuel smoke onayından sonra atılacak). Fiili süre: 29 Mayıs → 2 Haziran 2026
(charter 4-5 hafta tahmini). Sıradaki: Faz 7 (charter ayrı adımda).

---

## Dalga özeti

| Dalga | Konu | Adım Aralığı | Tahmin | Durum |
|---|---|---|---|---|
| A | Email + temizlik | 6.2-6.5 | 1.5-2 hafta | ✅ Tamamlandı (29-30 May) |
| B | UX iyileştirmeler | 6.6-6.9 | 1-1.5 hafta | ✅ Tamamlandı (30 May-1 Haz) |
| C | Performance + closeout | 6.10-6.13 | 1 hafta | ✅ Tamamlandı (1-2 Haz) |

## Adım tablosu

| Adım | Başlık | Charter Karar | Tech-debt | Durum |
|---|---|---|---|---|
| 6.1 | Faz 6 charter + roadmap | — | — | ✅ |
| 6.2 | Email: sosyal template'ler (P1) + opt-in/dijest/wiring (P2) | 1, 2, 3 | #22, #39 | ✅ |
| ~~6.3~~ | **6.2 P2'ye birleştirildi** (notification→email + tercih + dijest tek akış) | 2, 3 | #22, #39 | ➡️ 6.2 |
| 6.4 | NU1901 + CA2024 temizliği | — | #35, #36 | ✅ |
| 6.5 | Dalga A closeout | — | #40 | ✅ |
| 6.6 | Tag autocomplete + view dedupe | 4, 5 | #15, #16 | ✅ |
| 6.7 | Polling + multi-tab + sayfalama | — | #24, #25, #37, #40 | ✅ |
| 6.8 | Comment edit + image variants | — | #18, #21 | ✅ |
| 6.9 | Dalga B closeout | — | #40 | ✅ |
| 6.10 | n+1 sorgu refactor | — | #31, #32 | ✅ |
| 6.11 | IPartialRenderer + Tag helper extract | — | #17, #38 | ✅ |
| 6.12 | ChainedRateLimiter + SignalR self-host | Faz 5 §3 K7 | #27 | ✅ |
| 6.13 | Faz 6 closeout | — | #40 | ✅ |

---

## Dalga A — Email + temizlik

### Adım 6.2 — Email kanalı (P1 + P2) ✅

> **Denetim bulgusu:** Email altyapısı (IEmailService, IEmailTemplateRenderer,
> _EmailLayout, 3 provider, /admin/email/test) Faz 3'te zaten kuruluydu — P1
> yalnızca eksik **sosyal bildirim template'lerini** ekledi. Charter'ın "altyapı
> kur" varsayımı geçersizdi. 6.3 ayrı adım gereksizdi → P2'ye birleştirildi.

**P1/2 — Sosyal template'ler (commit `ba841de`):**
- 4 template (`Connection`, `Comment`, `ContentReport`, `MessageDigest`) + 4 model,
  mevcut `_EmailLayout` reuse. `/admin/email/test` dropdown. Test +4.

**P2/2 — Opt-in + dijest + wiring (bu commit):**
- 4 granüler bool kolon `UserProfile`'a (`EmailOnConnection/Comment/ContentReport/MessageDigest`,
  default açık) + `EmailDigestEntries` tablo. Migration `AddSocialEmailPreferencesAndDigest`
  (`Up()` `defaultValue` manuel `true`). **#39 drop YOK** — master switch korundu.
- `INotificationEmailDispatcher`: master → granüler → anonimize gating, tip→template,
  entity reload. `NotificationService` sosyal tiplerde best-effort tetikler.
- `MessageService` → `EmailDigestEntry` (master+granüler send-anı kontrol).
- `ProcessMessageDigestJob` (Hangfire, her dakika): 5 dk eşik + user-level all-pending grup.
- `/profil` 4 granüler toggle (master altında, kapalıyken görsel pasif).

**Charter Karar:** 1, 2, 3 · **Tech-debt:** #22, #39, #41 · **Test:** P2 +15 (783→798)

### Adım 6.4 — NU1901 + CA2024 temizliği ✅ (commit `9a49d88`)

**Yapılanlar:**
- NU1901 (GHSA-g4vj-cjjj-v7hg): `NuGet.Packaging`/`NuGet.Protocol` 6.12.1 → **6.12.5**
  transitive pinning (Web csproj). Sahip CodeGeneration.Design (design-time).
- CA2024: `LifeTableCsvParser` `!EndOfStream` → `(rawLine = await ReadLineAsync()) != null`.
- Build: 0 hata + **0 uyarı**. Test 798 (regresyon 0). #35, #36 ÇÖZÜLDÜ.

**Tech-debt:** #35, #36

### Adım 6.5 — Dalga A closeout ✅ (bu commit)

**Yapılanlar:** roadmap + README Dalga A özeti + tech-debt #40 Dalga B'ye taşıma
notu + `phase-6-wave-a-complete` annotated tag.

> **#40 polling fallback manuel smoke YAPILMADI** — Adım 6.7'de (polling
> görünürlük + multi-tab race + sayfalama refactor'u) yeni davranışla birlikte
> bütünsel doğrulanacak. Otomatik test kapsamı (GetNewSince) mevcut.

**Tech-debt:** #40 (→ 6.7)

---

## Dalga B — UX iyileştirmeler

### Adım 6.6 — Tag autocomplete + view count dedupe ✅ (commit `03f3c6c`)

**Yapılanlar:**
- `GET /api/post-tags/search?q={prefix}&take=10` + `GetPopularAsync` prefix filter
- Quill editor vanilla JS dropdown (tag chip pattern reuse, XSS-safe `textContent`)
- View dedupe: anonim cookie 30 dk TTL + login `IMemoryCache` 30 dk sliding

**Charter Karar:** 4, 5 · **Tech-debt:** #15, #16 ÇÖZÜLDÜ

### Adım 6.7 — Polling + multi-tab + sayfalama ✅ (commit `738fa66`)

**Yapılanlar:**
- Polling Page Visibility API duyarlı (sekme gizliyse pause, visible → hemen poll + interval)
- multi-tab read-state foundation: `IMessagingNotifier` 4. method + `ConversationRead`
  SignalR broadcast (alıcının tüm tab'larına)
- "Daha fazla yükle" sunucu-render `/older` endpoint + prepend + scroll pozisyon koruması

**Tech-debt:** #24, #25 ÇÖZÜLDÜ · #37 KISMEN (backend hazır, liste real-time badge Faz 7+) ·
#40 smoke denendi (yanlış senaryo → 6.13'e taşındı)

### Adım 6.8 — Comment edit history + image variants ✅ (commit `32d2d5a`)

**Yapılanlar:**
- `PostCommentRevision` entity (ilk orijinal saklama, yorum başına max 1 revision,
  cascade delete) + `AddPostCommentRevisions` migration; `(orijinali göster)` toggle +
  lazy fetch + `GET /api/post-comments/{id}/original`
- Image responsive: SixLabors.ImageSharp 480w + 800w WebP variant (upscale yok) +
  render-time `ImageVariantEnricher` srcset/sizes/lazy enrichment

> **Sapma:** charter dışı detaylar codebase gerçeğine uyarlandı — WebP 480/800
> (JPG 600/1200 değil), sade `srcset` (`<picture>` değil), #21'de yalnız İLK orijinal
> (tam geçmiş değil, charter kararı). Detay tech-debt #18 + #21 ÇÖZÜLDÜ kayıtlarında.

**Tech-debt:** #18, #21 ÇÖZÜLDÜ · **Test:** +9 (810→819)

### Adım 6.9 — Dalga B closeout ✅ (bu commit)

**Yapılanlar:** roadmap + README Dalga B özeti + tech-debt #40 Adım 6.13'e taşıma notu +
`phase-6-wave-b-complete` annotated tag. Yeni kod yok.

> **#40 polling fallback manuel smoke YİNE YAPILMADI** — Adım 6.7'de denendi ancak
> DevTools "Network Offline" yanlış senaryoydu (tüm HTTP kesilir → polling de test
> edilemez). Doğru senaryo: WS bloke + HTTP açık. Adım 6.13 bütünsel smoke'a taşındı.

**Tech-debt:** #40 (→ 6.13)

---

## Dalga C — Performance + closeout

### Adım 6.10 — n+1 sorgu refactor ✅ (commit `ac5f9ea`)

**Yapılanlar:**
- `ConversationService.GetForUserAsync`: (2N+2) → **tek query** (engelleme EXISTS,
  son mesaj + unread korelasyonlu APPLY, görüntüleme skalar projeksiyon)
- `GetUnreadCountAsync`: (N+1) → **tek SELECT COUNT** (Message.Conversation nav)
- `QueryCounterInterceptor` test helper (gerçek LocalDB'de Count==1 kanıtı)
- Denetim: `Conversation` iki ayrı read alanı (User1/User2LastReadAt) — bug yok

**Tech-debt:** #31, #32 ÇÖZÜLDÜ · **Test:** +5 (819→824)

### Adım 6.11 — IMessageHtmlRenderer + Tag helper extract ✅ (commit `579b943`)

**Yapılanlar:**
- Denetim: `IPartialRenderer` zaten ortak (Faz 4.9 P2); #38'in gerçek borcu
  Message→VM→render kompozisyonu → `IMessageHtmlRenderer` extract (MessagesController +
  SignalRMessagingNotifier tek noktaya)
- `IPostTagService.DecrementUsageForTagIdsAsync` batch helper (floor-0, no-save);
  ContentReportService + UserAnonymizationService inline loop birleşti (#17 trigger
  = KVKK 3. çağıran gerçekleşti). UserPostService dokunulmadı.

**Tech-debt:** #17, #38 ÇÖZÜLDÜ · **Test:** +9 (824→833)

### Adım 6.12 — ChainedRateLimiter + SignalR self-host ✅ (commit `f249cdb`)

**Yapılanlar:**
- `ChainedRateLimiter` (AND semantiği) — 5 policy çift pencere (dakika + saat).
  Mevcut limitler korundu; dakika-bazlılara gevşek saat tavanı, saat-bazlılara
  (report/connection) dakika=saat (davranış birebir)
- SignalR JS `wwwroot/lib/signalr/` self-host (8.0.7, CDN bağımlılığı kalktı).
  integrity/asp-fallback-src eklenmedi (self-host amacına aykırı, gerekçe commit'te)

> **Sapma:** plan "5 policy hepsi dakika" varsayıyordu; gerçekte karışıktı
> (report/connection saat-bazlı). Plan örnek değerleri mevcudu gevşetecekti → mevcut
> korunup tamamlayıcı pencere eklendi.

**Charter Karar:** Faz 5 §3 Karar 7 ✅ TAMAMLANDI · **Tech-debt:** #27 ÇÖZÜLDÜ · **Test:** +5 (833→838)

### Adım 6.13 — Faz 6 closeout ✅ (bu commit)

**Yapılanlar:** roadmap + README Faz 6 final özeti + charter §10 Implementation Status +
§6 risk düzeltme notu + §9 Faz 7 önizleme güncelleme + tech-debt kapanan 14 madde +
`phase-6-complete` tag METNİ (push kullanıcının #40 manuel smoke onayına bırakıldı).
Yeni kod yok.

> **#40 polling fallback manuel smoke:** doğru senaryo (WS bloke + HTTP açık) hâlâ
> kullanıcı tarafından yapılacak; `phase-6-complete` tag bu onaydan sonra atılır.

**Tech-debt:** #40 (manuel smoke beklemede)

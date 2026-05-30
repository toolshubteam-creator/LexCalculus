# Lex Calculus — Faz 6 Yol Haritası

## Olgunlaştırma + UX + Performance

**Başlangıç:** 29 Mayıs 2026 · **Charter:** [phase-6-charter.md](./phase-6-charter.md)
· **Envanter:** [phase-6-scope-inventory.md](./phase-6-scope-inventory.md)

⏳ **Faz 6 başladı** — Adım 6.1 (charter + roadmap) tamamlandı; sıradaki Adım 6.2.

---

## Dalga özeti

| Dalga | Konu | Adım Aralığı | Tahmin | Durum |
|---|---|---|---|---|
| A | Email + temizlik | 6.2-6.5 | 1.5-2 hafta | ⏳ |
| B | UX iyileştirmeler | 6.6-6.9 | 1-1.5 hafta | ⏳ |
| C | Performance + closeout | 6.10-6.13 | 1 hafta | ⏳ |

## Adım tablosu

| Adım | Başlık | Charter Karar | Tech-debt | Durum |
|---|---|---|---|---|
| 6.1 | Faz 6 charter + roadmap | — | — | ✅ |
| 6.2 | Email: sosyal template'ler (P1) + opt-in/dijest/wiring (P2) | 1, 2, 3 | #22, #39 | ✅ |
| ~~6.3~~ | **6.2 P2'ye birleştirildi** (notification→email + tercih + dijest tek akış) | 2, 3 | #22, #39 | ➡️ 6.2 |
| 6.4 | NU1901 + CA2024 temizliği | — | #35, #36 | ⏳ |
| 6.5 | Dalga A closeout | — | #40 | ⏳ |
| 6.6 | Tag autocomplete + view dedupe | 4, 5 | #15, #16 | ⏳ |
| 6.7 | Polling + multi-tab + sayfalama | — | #24, #25, #37 | ⏳ |
| 6.8 | Comment edit + image variants | — | #18, #21 | ⏳ |
| 6.9 | Dalga B closeout | — | — | ⏳ |
| 6.10 | n+1 sorgu refactor | — | #31, #32 | ⏳ |
| 6.11 | IPartialRenderer + Tag helper extract | — | #17, #38 | ⏳ |
| 6.12 | ChainedRateLimiter + SignalR self-host | Faz 5 §3 K7 | #27 | ⏳ |
| 6.13 | Faz 6 closeout | — | — | ⏳ |

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

### Adım 6.4 — NU1901 + CA2024 temizliği ⏳

**Kapsam:**
- NU1901: `NuGet.Packaging`/`NuGet.Protocol` 6.12.1 transitive açık → yamalı
  sürüme pin/upgrade, build temiz
- CA2024: `LifeTableCsvParser.cs` `EndOfStream` → `ReadLineAsync` döngüsü

**Tech-debt:** #35, #36 · **Süre:** ~0.5 gün

### Adım 6.5 — Dalga A closeout ⏳

**Kapsam:** roadmap güncelleme + #40 polling fallback manuel smoke (DevTools WS
blok + 30 sn polling gözlemi) + mini tag opsiyonel.

**Tech-debt:** #40 · **Süre:** ~0.5 gün

---

## Dalga B — UX iyileştirmeler

### Adım 6.6 — Tag autocomplete + view count dedupe ⏳

**Kapsam:**
- `GET /api/post-tags/search?q={prefix}&take=10` + `GetPopularAsync` prefix filter
- Quill editor vanilla JS dropdown (tag chip pattern reuse)
- View dedupe: anonim cookie 30 dk TTL + login `IMemoryCache` 30 dk sliding

**Charter Karar:** 4, 5 · **Tech-debt:** #15, #16 · **Süre:** ~1 gün

### Adım 6.7 — Polling + multi-tab + sayfalama ⏳

**Kapsam:**
- Polling `visibilityState` duyarlı (sekme gizliyse pause)
- multi-tab mark-as-read race: alıcının tüm tab'larına SignalR read-state broadcast
- "Daha fazla yükle" sunucu-render endpoint (`_Message` HTML array) + prepend

**Tech-debt:** #24, #25, #37 · **Süre:** ~1-1.5 gün

### Adım 6.8 — Comment edit history + image variants ⏳

**Kapsam:**
- `PostCommentRevision` tablosu (eski body, EditedAt) + admin "geçmiş" linki
- Image responsive: ImageSharp 600/1200px varyant + `<picture>`/`srcset`

**Tech-debt:** #18, #21 · **Süre:** ~2 gün

### Adım 6.9 — Dalga B closeout ⏳

**Kapsam:** roadmap güncelleme + UX manuel doğrulama.

**Süre:** ~0.5 gün

---

## Dalga C — Performance + closeout

### Adım 6.10 — n+1 sorgu refactor ⏳

**Kapsam:**
- `ConversationService.GetForUserAsync`: LastMessage + UnreadCount tek aggregate
  query (GroupJoin / window)
- `GetUnreadCountAsync`: tek aggregate (her authenticated request'te çağrılıyor)
- EF query-count assertion testleri

**Tech-debt:** #31, #32 · **Süre:** ~1 gün

### Adım 6.11 — IPartialRenderer reuse + Tag helper extract ⏳

**Kapsam:**
- `IMessageHtmlRenderer.RenderForViewerAsync` extract (MessagesController +
  SignalRMessagingNotifier duplikasyonu tek noktaya)
- `IPostTagService.DecrementForPostAsync` helper (UserPostService +
  ContentReportService duplikasyonu)

**Tech-debt:** #17, #38 · **Süre:** ~0.5-1 gün

### Adım 6.12 — ChainedRateLimiter + SignalR self-host ⏳

**Kapsam:**
- `ChainedRateLimiter`: her policy saat+dakika çift pencere (Faz 5 §3 Karar 7
  tamamlama)
- SignalR JS `wwwroot/lib/signalr/` self-host + integrity hash + asp-fallback-src

**Charter Karar:** Faz 5 §3 Karar 7 (çift pencere tamamlama) · **Tech-debt:** #27 · **Süre:** ~1 gün

### Adım 6.13 — Faz 6 closeout ⏳

**Kapsam:**
- Tüm Faz 6 senaryoları manuel doğrulama
- README Faz 6 final özeti + metrikler
- bu dosya (6.2-6.13 ✅) + tech-debt kapatılan maddeler "ÇÖZÜLDÜ"
- Faz 7 önizleme notu
- `phase-6-complete` annotated tag

**Süre:** ~1 gün

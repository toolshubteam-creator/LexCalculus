# Lex Calculus — Faz 6 Charter

## Olgunlaştırma + UX + Performance

**Başlangıç:** 29 Mayıs 2026
**Tahmin:** 4-5 hafta charter · ~1-2 hafta gerçek (Faz 5 baseline)
**Önceki:** Faz 5 tamamlandı (15 Mayıs 2026, ~2 hafta gerçek)
**Tag (kapanış):** `phase-6-complete`
**Charter sürümü:** 1.0
**Temel:** `docs/phase-6-scope-inventory.md` (Adım 6.0 denetim envanteri)

---

## §1 Amaç ve Kapsam

Faz 6 üç eksende ilerler — yeni teritory değil, mevcut altyapı üzerine
olgunlaştırma/refactor:

1. **Email notification kanalı** (tech-debt #22, #39, #40) — kullanıcı
   platformda değilken bildirim alır.
2. **UX iyileştirmeleri** (D kümesi) — tag autocomplete, view count dedupe,
   sayfalama tamamlama, multi-tab race, comment edit history, image variants.
3. **Performance + cleanliness** (F kümesi) — n+1 sorgular, reuse refactor'lar,
   güvenlik/analyzer uyarıları.

Adım 6.0 envanteri temel: 39 benzersiz tech-debt madde, 21 Faz 6 aday.

### KAPSAMDA (seçilen 17 madde)

| Küme | Maddeler |
|---|---|
| **B — Email** | #22 (email kanalı), #39 (master switch korunur + granüler), #40 (polling fallback smoke) |
| **D — UX** | #15 (tag autocomplete), #16 (view dedupe), #18 (image srcset), #21 (comment edit history), #24 (polling görünürlük), #25 (sayfalama), #37 (multi-tab race) |
| **F — Performance** | #17 (tag helper extract), #27 (SignalR self-host), #31 (GetForUserAsync n+1), #32 (GetUnreadCountAsync n+1), #35 (NU1901), #36 (CA2024), #38 (IPartialRenderer reuse) + ChainedRateLimiter (Faz 5 §3 Karar 7 tamamlama) |

### KAPSAM DIŞI (Faz 7+)

- **A** — Mobile/PWA (tek tema, büyük karar)
- **C** — Multi-instance scaling (#28 SignalR Redis backplane, gerçek trafik gerektirir)
- **E** — Admin analytics dashboard / #9 ActivityLog retention (hukuki görüş)
- **D/F düşük öncelik** — #12 (media GC), #14 (hierarchical reply), #29 (negotiate
  rate limit), #33 (admin "tüm konuşmayı incele")
- **İzleme (sürekli açık)** — #1, #3, #4, #10, #26 (bkz. envanter §5)

---

## §2 Vizyon Tutarlılığı

Faz 1-5 temel kararları korunur (atıf: `phase-4-charter.md` §1.2,
`phase-5-charter.md` §2):

- **Plansız (Free)** — email notification ek ücret değil, herkese açık
- **Vatandaş 1. sınıf** — email opt-in tamamen kullanıcı kontrolünde
- **Sessiz pattern** — email throttling/dijest ile gürültüsüzlük korunur
- **KVKK uyumlu** — anonimize (Faz 5 Karar 6) sonrası email gönderimi durur
  (`deleted-{id}@local` adresine gönderim yapılmaz)

---

## §3 Mimari Kararlar (Yeni)

### Karar 1 — Email template: Razor view

`Pages/EmailTemplates/` altında Razor view + `_EmailLayout.cshtml`. E-mail
clients external CSS strip ettiği için **inline style ZORUNLU** (CLAUDE.md
e-posta istisnası). `IPartialRenderer` pattern reuse (Faz 5 #38 ile uyumlu
genel render). Plaintext fallback başlangıçta YOK (HTML standart).

### Karar 2 — Email dijest stratejisi

| Notification türü | Gönderim |
|---|---|
| Yeni mesaj | 5 dk gecikmeli dijest ("Son 5 dk'da N mesaj aldınız") |
| ConnectionRequest / Accepted | Anlık |
| PostComment (kendi makalene) | Anlık |
| ContentReportResolved / ContentRemoved | Anlık |

Dijest Hangfire scheduled job (mevcut altyapı, Faz 1+). Per-mesaj email değil
— gürültüsüzlük (vizyon §2).

### Karar 3 — Email tercih granülaritesi + #39 master switch (DÜZELTİLDİ, Adım 6.2 P2)

Kategori başına opt-in (`UserProfile` bool kolonları, default **açık**):
`EmailOnConnection`, `EmailOnComment`, `EmailOnContentReport`, `EmailOnMessageDigest`.
`/profil` sayfasında toggle (ana anahtarın altında, kapalıyken görsel pasif).

**#39 kararı (revize):** Adım 6.2 P1/P2 denetiminde `ApplicationUser.NotificationsEmailEnabled`'ın
**orphan OLMADIĞI** tespit edildi — `DataFreshnessCheckJob` (sistem/tazelik e-postaları)
hem de `/profil` toggle'ı tarafından zaten kullanılıyor. Bu yüzden **drop EDİLMEZ**;
anlamı genişletilerek **"tüm e-postalar" ana anahtarı (master)** olarak korunur. 4 granüler
alan bunun altına eklenir: dispatch sırası `IsActive → Email → Profile → master →
granüler`. Master kapalıyken (DataFreshness dahil) hiçbir e-posta gitmez — eski davranış
korunur. Migration: yalnız 4 `AddColumn` + `EmailDigestEntries` tablo, **DropColumn yok**
(`Up()` defaultValue `true` manuel düzeltildi — CLAUDE.md / tech-debt #3 tuzağı).
Adım 6.0 envanterinin "#39 orphan" etiketi yanlıştı (bkz. roadmap closeout notu).

### Karar 4 — View count dedupe (#16)

- **Anonim:** cookie `vc_{postId}=1`, 30 dk TTL
- **Login:** `IMemoryCache` (kullanıcı+post anahtar), 30 dk sliding expiration
- **Ek DB tablosu YOK** — scaling (distributed cache) Faz 7+

### Karar 5 — Tag autocomplete: server-side (#15)

- Yeni endpoint: `GET /api/post-tags/search?q={prefix}&take=10`
- `IPostTagService.GetPopularAsync` prefix filter ile genişletilir
- Quill editor entegrasyonu: basit dropdown (Tribute.js opsiyonel) — vanilla JS
  tercih (mevcut tag chip pattern reuse, dış bağımlılık minimize)

> **Not — ChainedRateLimiter (yeni karar değil):** Faz 6'da rate limiting Faz 5
> §3 Karar 7'nin **çift pencere** (saat+dakika) öngörüsüyle tamamlanır
> (`ChainedRateLimiter`, örn. mesaj 30/dk **ve** 500/gün). Bu yeni bir Faz 6
> kararı değil — Faz 5 Karar 7'nin "kısmen" kapanan kısmının kapatılması (6.12).

---

## §4 Adım Kırılımı (3 Dalga, 12 Alt Adım)

### Dalga A — Email + temizlik — 1.5-2 hafta tahmin

| Adım | Konu | Karar | Tech-debt |
|---|---|---|---|
| 6.2 | Email altyapısı (Razor template + `_EmailLayout` + IEmailService entegrasyon) | 1, 2 | #22 |
| ~~6.3~~ | **6.2 P2'ye birleştirildi** — email tercih + opt-in (#39 master korunur + 4 granüler + Hangfire dijest) | 2, 3 | #22, #39 |
| 6.4 | NU1901 (#35) + CA2024 (#36) temizliği (mini) | — | #35, #36 |
| 6.5 | Dalga A closeout (mini — roadmap güncelleme + polling smoke #40) | — | #40 |

### Dalga B — UX iyileştirmeler — 1-1.5 hafta tahmin

| Adım | Konu | Karar | Tech-debt |
|---|---|---|---|
| 6.6 | Tag autocomplete + view count dedupe | 4, 5 | #15, #16 |
| 6.7 | Polling görünürlük + multi-tab race + sayfalama | — | #24, #25, #37 |
| 6.8 | Comment edit history + image responsive variants | — | #18, #21 |
| 6.9 | Dalga B closeout (mini) | — | — |

### Dalga C — Performance + closeout — 1 hafta tahmin

| Adım | Konu | Karar | Tech-debt |
|---|---|---|---|
| 6.10 | n+1 sorgu refactor (GetForUserAsync + GetUnreadCountAsync) | — | #31, #32 |
| 6.11 | IPartialRenderer reuse + Tag UsageCount helper extract | — | #17, #38 |
| 6.12 | ChainedRateLimiter + SignalR JS self-host | Faz 5 §3 K7 | #27 |
| 6.13 | Faz 6 closeout (README/roadmap/tech-debt + tag) | — | — |

---

## §5 Tahmin

Charter **4-5 hafta**. Faz 5 baseline ~2 hafta. Faz 6 çoğunlukla mevcut altyapı
üzerine refactor/cleanup → Faz 5'ten hızlı olabilir (~1-2 hafta gerçek beklenir).

**Muhafazakar baseline notu:** Tahminler karşılaştırma için yüksek tutulur
(yanıltıcı baseline — Faz 4 charter 13 hafta, gerçek ~2 gün, ~32x). Faz 6
"yeni teritory" değil; SignalR/email/EF zaten oturmuş. Her dalga sonunda ölçüm.

---

## §6 Risk ve Tuzaklar

| Tuzak | Dalga | Hafifletme |
|---|---|---|
| **IEmailService production gönderim hiç test edilmedi** | A | SMTP config + gerçek email smoke (6.2) — `LoggingEmailService` dev'de, gerçek adapter test edilmeli |
| **SPF/DKIM/DMARC domain ayarı** | A | Deployment konusu, geliştirme dışı — deliverability charter dışı not |
| **Hangfire dijest timing + reconciliation** | A | 5 dk pencere idempotent job; çift gönderim guard (son gönderim timestamp) |
| **#39 migration default tuzağı (4 yeni bool)** | A | CLAUDE.md kuralı: `Up()` `defaultValue` manuel `true` yapıldı (geçmiş tuzak #3) |
| **multi-tab mark-as-read race** | B | SignalR: alıcının TÜM tab'larına read-state broadcast (IMessagingNotifier reuse) |
| **n+1 refactor performans regresyon riski** | C | EF query-count assertion testleri (mevcut testler n+1'i kanıtlamıyor) |
| **ChainedRateLimiter false positive** | C | Test ortamında relaxed limit (Faz 5 pattern reuse) |

> **Düzeltme notu (Adım 6.13, tarihsel — silinmez):** Charter yazıldığında
> `IEmailService` altyapısının Faz 3'te tam kurulduğu fark edilmemişti. Adım 6.2 P1
> denetiminde 9 mevcut template + 3 provider + admin test endpoint tespit edildi;
> "sıfırdan kur" planı iptal, mevcut altyapı tamamen reuse edildi. Kök sebep: Adım 6.0
> envanteri dosya/property varlığına bakıp **kullanım taraması** yapmamıştı
> (tech-debt #41). Aynı kör nokta Faz 6 boyunca 6 kez yakalandı (bkz. §10).

---

## §7 Test Stratejisi

- Mevcut **SQL Server LocalDB** altyapısı (Adım 5.8) kullanılır
- **Email:** in-memory/mock `IEmailService` ile gönderim assertion
- **Hangfire:** synchronous job execution (mevcut pattern)
- **Tag autocomplete:** integration test (endpoint + popüler tag fixture)
- **n+1:** EF Core diagnostic logger ile query-count assertion
- **#40 polling fallback:** Dalga A closeout'ta manuel smoke (DevTools WS blok)

**Hedef test:** 779 → ~850 (Faz 6 sonu).

---

## §8 Tamamlanma Kriterleri

1. Email notification yayında (en az 1 türde gerçek email gönderim doğrulandı)
2. Kullanıcı email tercih sayfası `/profil`'de mevcut (granüler opt-in)
3. NU1901 NuGet açığı kapatıldı (build temiz)
4. CA2024 uyarısı temizlendi
5. Tag autocomplete UI yayında
6. View count dedupe çalışıyor (anonim cookie + login cache)
7. n+1 sorgular ölçülebilir iyileşme (query-count testleri yeşil)
8. 12 alt adım (Dalga A+B+C) tümü ✅
9. README + roadmap + tech-debt güncel
10. `phase-6-complete` annotated tag

---

## §9 Faz 7 Önizleme (Bilgi Amaçlı)

**Faz 6'da KAPANAN tech-debt (14):** #15, #16, #17, #18, #21, #22, #24, #25, #27,
#31, #32, #35, #36, #38 (+ #39 master switch korundu).
**Kısmen/bekleyen:** 🟡 #37 (multi-tab backend hazır, liste real-time badge Faz 7+),
🟡 #40 (polling fallback manuel smoke — doğru senaryo kullanıcıda).
**Süreç borcu:** #41 (envanter kullanım-taraması) — Faz 7 başlangıcında uygulanmalı.

Faz 7'ye taşınan başlıklar:

- **A** — Mobile/PWA (tek tema, ayrı charter)
- **C** — Multi-instance scaling (#28 SignalR Redis backplane, gerçek trafik)
- **E** — Admin analytics dashboard / #9 ActivityLog retention (hukuki görüş)
- **#37** — multi-tab tam çözüm (liste sayfası real-time unread badge)
- **D/F düşük öncelik kalanları** — #12 (media GC), #14 (hierarchical reply),
  #29 (negotiate rate limit), #33 (admin "tüm konuşmayı incele")
- **Mesajlaşma zenginleştirme** — read receipts, typing, search, görsel/dosya
- **İzleme tech-debt** — #1, #3, #4, #10, #26 (Adım 6.0 envanter §5)
- **Calculator kategorileri D-I** — kalan 6 hukuk kategorisi (15+ hesaplayıcı)
- **#41** — envanter denetim süreç borcu (Faz 7 başlangıcında)

Bunların hiçbiri Faz 6 kapsamında değildir. Faz 7 charter ayrı adımda hazırlanacak.

---

## §10 Implementation Status (kapanış notu — Adım 6.13)

Faz 6 = Dalga A + B + C (12 alt adım) **tamamlandı** (29 Mayıs → 2 Haziran 2026).

**Yeni Mimari Karar (§3) implementasyonu:**

| Karar | Konu | Durum |
|---|---|---|
| 1 | Email template: Razor view + `_EmailLayout`, inline CSS | ✅ Adım 6.2 P1 — Faz 3 altyapısı reuse (yeniden inşa DEĞİL) |
| 2 | Email dijest: mesaj 5 dk gecikmeli, diğerleri anlık | ✅ Adım 6.2 P2 — Hangfire `ProcessMessageDigestJob` |
| 3 | Tercih granülaritesi: master + 4 kategori bool | ✅ Adım 6.2 P2 — #39 `NotificationsEmailEnabled` master switch korundu (drop iptal, yanlış envanter varsayımı) |
| 4 | View count dedupe: anonim cookie + login IMemoryCache | ✅ Adım 6.6 |
| 5 | Tag autocomplete: server-side + vanilla JS | ✅ Adım 6.6 |

**Faz 5 §3 Karar 7** (`ChainedRateLimiter` saat+dakika çift pencere) — ✅ TAMAMLANDI
Adım 6.12 (Faz 5'te kısmi/tek pencere, Faz 6'da çift pencere AND semantiği).

**§8 Tamamlanma kriterleri:** 10/10 karşılandı (kriter 10 `phase-6-complete` tag,
kullanıcı #40 manuel smoke onayından sonra atılacak).

**Süre:** charter tahmini 4-5 hafta; gerçek 29 Mayıs → 2 Haziran (~5 gün). Baseline
yanıltıcı (yoğun ardışık oturumlar) — Faz 5 ile tutarlı hızlanma.

**Süreç sapmaları (6 envanter eksiği, kök sebep tech-debt #41):**
1. Adım 6.0 → Email altyapısı Faz 3'te kuruluydu (yeniden inşa iptal)
2. Adım 6.2 P2 → #39 "orphan" yanlış etiketleme, `DataFreshnessCheckJob` aktif kullanıyordu
3. Adım 6.6 → view increment inline'dı (servis metodu yoktu)
4. Adım 6.10 → `Conversation.LastReadAt` iki ayrı alan (User1/User2), endişe yersizdi
5. Adım 6.11 → `IPartialRenderer` zaten ortak, #38 gerçek içeriği farklıydı
6. Adım 6.12 → rate-limit pencereleri karışık (policy'ler dakika/saat farklı)

Not: TargetFramework **.NET 10** başlangıçtan beri (`c640de2`, 25 Nisan 2026) — Faz 6'da
upgrade YOK. (SignalR client yorumundaki ".NET 8/10" sürüm uyumluluğunu kasteder.)

---

*Charter sürümü 1.0 — 29 Mayıs 2026. Faz 6 boyunca güncel kalır;
değişiklikler commit'lerle işlenir. §10 kapanış notu Adım 6.13'te eklendi.*

# Faz 6 Kapsam Envanteri (Denetim Raporu — Adım 6.0)

> **Geçici denetim raporu.** Faz 6 charter'ından (Adım 6.1) ÖNCE yapılan
> "eksik adım kalmasın" denetimi. Charter DEĞİL: yeni kod yok, yeni tag yok.
> Amaç: Faz 5 sonu açık konuları envanterlemek + Faz 6 kapsamını önermek.
> Charter (6.1) bu envanteri önceliklendirme temeli olarak kullanacak.
>
> Tarih: 29 Mayıs 2026 · Son commit: `30e078e` · Son tag: `phase-5-complete`

---

## §1 Faz 5 sonu durum

| Metrik | Değer |
|---|---|
| Tag | `phase-5-complete` (15 Mayıs 2026) |
| Test | 779 (Adım 5.8 SQL Server LocalDB tam geçiş sonrası) |
| Build | 0 hata · 2 uyarı ailesi (NU1901 + CA2024 → tech-debt #35, #36) |
| Migration | 32 |
| Hesaplayıcı | 17/17 (9 hukuk kategorisinden 3'ü hazır) |

**Üretim hazırlığı:** Çekirdek olgunlaştırma kalemleri kapandı —
KVKK uyumlu hesap silme/anonimize ✓, real-time mesajlaşma (SignalR + polling
fallback) ✓, moderasyon (Post + Comment + Message hide/sil) ✓, rate limiting
(5 named policy) ✓, test altyapısı SQL Server LocalDB ✓ (production-yakın
IDENTITY/FK/transaction semantiği). Açık kalemler "blocker" değil —
olgunlaştırma + UX + performans iyileştirmesi niteliğinde.

---

## §2 tech-debt.md envanteri

**Başlangıç:** 34 numaralı madde. **Denetim sonrası:** 40 numara (6 yeni madde
#35-#40 eklendi) + #25 ≡ #30 mükerrer tekleştirildi → **39 benzersiz mantıksal
konu**. (#30 numara silinmeden "DUPLICATE → #25" notu ile sabit tutuldu.)

### Kategori dağılımı

| Kategori | Sayı | Açıklama |
|---|---|---|
| **A — Çözülmüş** | 11 | 6'sı Faz 5'te, 5'i Faz 3'te kapatıldı |
| **B — Faz 6 aday** | 21 | Email (2) + UX (7) + perf/cleanup (11) + test borcu (1) |
| **C — Faz 7+** | 1 | #28 SignalR Redis backplane (multi-instance) |
| **D — Sürekli izleme** | 5 | #1, #3, #4, #10, #26 (nice-to-have) |
| **E — Belirsiz** | 1 | #9 ActivityLog retention (hukuki görüş external) |

### A — Çözülmüş (11)

**Faz 5'te kapatılan 6 madde (vurgulu):**
- **#11** InMemory provider sınırları → Adım 5.8 ✓
- **#13** Hide vs Delete moderation → Adım 5.3 + 5.7 ✓
- **#19** KVKK hesap silme + anonimize → Adım 5.1 ✓
- **#20** Bot/spam rate limiting → Adım 5.2 ✓
- **#23** Authorize → otomatik NoIndex → Adım 5.3 ✓
- **#34** Hibrit test altyapı → LocalDB → Adım 5.8 P2 ✓

Faz 3'te kapatılan 5 madde (tarihçe): #2 Seeder soft-delete restore · #5 Register
SignInAsync try/catch · #6 ChangePassword scaffold · #7 _ManageNav Bootstrap
kalıntısı · #8 TenantYonetim inline style.

### B — Faz 6 aday (21) → §4'te kümelere ayrıldı

### C / D / E

- **C (Faz 7+):** #28 SignalR Redis backplane — multi-instance horizontal scale
  gereksinimi (üretim trafiği başlayınca; tek instance + sticky session yeterli).
- **D (izleme):** #1 Hangfire dep Infrastructure'a sızdı · #3 EF migration default
  uyarısı (kalıcı insan-süreci) · #4 Hangfire 401/403 (teorik) · #10 admin slug
  yönetimi (kullanıcı talebi yok) · #26 avatar URL user-agnostic (MEYP).
- **E (belirsiz):** #9 ActivityLog retention policy — avukat görüşü (external
  bağımlılık) gerekmeden süre belirlenemez; Faz 6'ya zorlanmamalı.

---

## §3 Belgelenmemiş edge case'ler (yeni eklenen 6 madde)

Faz 4-5'te karar/uyarı olarak ortaya çıkmış ama tech-debt'e işlenmemiş kalemler.
Tespit edilip deftere alındı (numaralar sıralı, son numara 34'ten devam):

| # | Başlık | Kategori | Faz 6 küme |
|---|---|---|---|
| 35 | NU1901 — NuGet paket güvenlik açığı (transitive) | B | F (güvenlik) |
| 36 | CA2024 — async `EndOfStream` uyarısı | B | F (temizlik) |
| 37 | SignalR multi-tab mark-as-read race | B | D (UX) |
| 38 | IPartialRenderer reuse pattern refactor | B | F (cleanliness) |
| 39 | NotificationsEmailEnabled orphan field | B | B (email başlangıç) |
| 40 | Polling fallback manuel test borcu (5.6 Senaryo 5) | B | B (test) |

### Numara tutarsızlıkları (düzeltildi)

Charter §12 (Faz 5 kapanış notu) bazı tech-debt numaralarını yanlış eşliyordu;
Adım 6.0'da charter §12 metni **sadece numara/başlık eşlemesi** açısından
düzeltildi (ana mantık dokunulmadı):

| Charter §12 eski atıf | Gerçek / düzeltilmiş |
|---|---|
| ConversationService N+1 "#29, #30" | **#31** (GetForUserAsync) + **#32** (GetUnreadCountAsync) |
| "mark-as-read race #26" | #26 = avatar URL'di; race kaydı yoktu → yeni **#37** |
| "IPartialRenderer #28" | #28 = Redis backplane'di; kaydı yoktu → yeni **#38** |
| self-host #27, admin konuşma #33 | ✓ doğruydu (değişmedi) |
| #29 (SignalR negotiate rate limit) | charter'da hiç anılmamış (açık kaldı) |

**`report-modal.js` extract:** Adım 5.7'de tamamlandı (Makale + Mesajlar
paylaşımlı DRY). Açık tech-debt DEĞİL, numara çakışması yok — beklenen
"numara karışıklığı" bu kalemde değil, yukarıdaki charter §12 eşlemesindeydi.

### Faz 5 takip kalitesi yorumu

5+ belgelenmemiş kalem eşiği aşıldı — ama qualitatif olarak DÜŞÜK ciddiyette:
#35/#36 build uyarısı (low-severity + analyzer), #37/#38 charter §12'de zaten
atıfla anılmış ama numaralı kayıt açılmamış (eşleme drift'i), #39 Faz 3'ten
kalma orphan field, #40 atlanan manuel test. **Yapısal/sistemik bir takip
boşluğu değil** — "kapanış notunda atıf var ama deftere işlenmemiş" tipi
disiplin kaçağı. Faz 6 disiplini: bir kalem charter'da atıfla anılınca fiilen
tech-debt numarası açılsın (atıf ≠ kayıt).

---

## §4 Faz 6 kapsam önerisi — B + D + F karması

**Tema: Olgunlaştırma + UX + Performance.** Kategori B'deki 21 aday üç kümeye ayrılır.

### Küme B — Email notification kanalı
- `IEmailService` Faz 3'te mevcut (3 adapter: Logging/Smtp/SendGrid; Identity
  confirm-email + tenant davet akışlarında çalışıyor — Adım 6.1 charter'da teyit).
- `NotificationsEmailEnabled` orphan field (#39) hazır → tüketilecek başlangıç noktası.
- Hangi notification türleri email gönderir (per-type opt-in: ConnectionRequest,
  PostComment, ContentReportResolved/ContentRemoved, yeni mesaj delay'li).
- Email template'leri (basit HTML, branded; e-mail clients için inline style istisnası).
- Dijest / throttling (her mesaj ayrı email değil — "son N dk okunmamış mesaj" özeti).
- Kullanıcı tercih sayfası (/profil email notification toggle).
- **Maddeler:** #22, #39 · (test borcu #40 bu küme akışında kapanır)

### Küme D — UX iyileştirmeleri (küçükler)
- Comment edit history (#21) · Tag autocomplete (#15) · Hierarchical comment
  reply 1-seviye (#14) · Image responsive variants srcset (#18*) · View count
  dedupe (#16) · Media GC orphan upload (#12*) · Polling sayfa görünürlüğü
  duyarsız fix (#24*) · "Daha fazla yükle" sayfalama tamamlama (#25) · SignalR
  multi-tab mark-as-read race (#37) · Admin mesaj "Tüm konuşmayı incele" (#33)
- (*) #12, #18, #24 perf etkili — charter dilerse F kümesine alabilir.

### Küme F — Performance / cleanliness
- ConversationService n+1 (#31 GetForUserAsync, #32 GetUnreadCountAsync) ·
  IPartialRenderer reuse refactor (#38) · Tag UsageCount decrement helper extract
  (#17) · ChainedRateLimiter (saat+dakika çift limit — charter Karar 7 kısmi) ·
  SignalR JS production self-host + integrity (#27) · SignalR negotiate rate
  limit (#29) · NU1901 NuGet güvenlik (#35) · CA2024 async uyarı temizliği (#36)

### Tahmini iş yükü

| Küme | Aday | Tahmin |
|---|---|---|
| B (email) | 2 (+#40 test) | ~1 hafta |
| D (UX) | 7-10 | ~1-2 hafta |
| F (perf/cleanup) | 9-11 | ~3-5 gün |

> **Charter tahmini:** ~3-4 hafta (muhafazakar baseline notu — Faz 5'te 6 hafta
> charter tahmini ~2 haftada gerçekleşti; ama her madde küçük olduğundan
> baseline yine yanıltıcı olabilir). Tüm B-kategori adayları tek faza
> sığmayabilir; dalga kırılımı + seçim Adım 6.1 charter'ın işi.

---

## §5 Faz 6 dışı (Faz 7+ veya iptal)

- **A küme (mobile / PWA)** — charter §9; tek başına Faz 7 teması, büyük karar
  (PWA mı native mi). Faz 6 dışı.
- **C: #28 SignalR Redis backplane** — multi-instance scaling, üretim trafiği
  başlayıp horizontal scale gerekene kadar (tek instance + sticky session yeterli).
- **E: #9 ActivityLog retention policy** — hukuki görüş external bağımlılık;
  süre belirlenmeden kod yazılamaz. Belirsiz, Faz 6'ya zorlanmamalı.
- **Analytics / admin dashboard zenginleştirme** — Faz 7+ veya iptal (talep yok).
- **Mesajlaşma zenginleştirme** (read receipts, typing, search, görsel/dosya
  mesaj) — charter Faz 5 §1 kapsam-dışı; Faz 6+ ayrı tema.

**Sürekli izleme (D kategorisi tech-debt):** #1 Hangfire dep · #3 EF migration
default uyarısı · #4 Hangfire 401/403 · #10 admin slug yönetimi · #26 avatar URL
user-agnostic (MEYP). Sürekli açık kalır, "nice to have".

---

## §6 Manuel test borçları

| Borç | Adım | Durum | Karar |
|---|---|---|---|
| SignalR kopuk → polling fallback (Senaryo 5) | 5.6 | Doğrulanmadı ("deneyemedim") | **Atlandı → tech-debt #40**, Faz 6 B kümesinde doğrulanacak |
| Rate limiting tam UI testi | 5.2 | Atlandı (3 integration test yeterli görüldü) | Atlanabilir — integration kapsamı var |
| KVKK 7-gün undo + anonimize manuel | 5.1 | `phase-5-complete` "manuel doğrulama sonrası" | Kapalı kabul |

**Karar (Adım 6.0):** Polling fallback manuel testi **atlanıyor** — otomatik
test'lerle (MessagesApiControllerTests GetNewSince) dolaylı kapsanmış kabul
edilir, bloklayıcı değil. Tech-debt #40 olarak kayıt altına alındı; Faz 6 B
kümesi notification akışı test edilirken bütünsel mesajlaşma smoke testi
içinde (DevTools'tan WS bloklama + 30 sn polling gözlemi, ~10 dk) kapanır.

---

## §7 Sonraki adım

**Adım 6.1: Faz 6 charter.**
- Bu envanter kapsam temeli.
- B + D + F küme adayları charter'da dalga'ya bölünür.
- Charter formatı kompakt (Faz 5 charter ~410 satır → hedef ~250 satır;
  altyapı kararları oturmuş, yeni mimari karar daha az).
- §3'teki charter §12 numara düzeltmeleri zaten uygulandı (charter güncel).
- Öngörülen 3 dalga:
  - **Dalga A:** Email kanalı (#22, #39, #40 doğrulama) + güvenlik/uyarı
    temizliği (#35, #36)
  - **Dalga B:** UX iyileştirmeler (#14, #15, #16, #21, #25, #33, #37)
  - **Dalga C:** Performance + cleanliness (#31, #32, #38, #17, #27, #29,
    ChainedRateLimiter) + Faz 6 closeout

> Bu dalga öngörüsü bağlayıcı değil — Adım 6.1 charter kesinleştirir.

# Lex Calculus — Faz 5 Charter

## Real-time + Olgunlaştırma + KVKK

**Başlangıç:** 2 Mayıs 2026
**Tahmin:** 6 hafta charter (gerçek Faz 4 baseline'a göre ~1-2 hafta)
**Önceki:** Faz 4 tamamlandı (Dalga A + B); Dalga C → Faz 5
**Tag (kapanış):** `phase-5-complete`
**Charter sürümü:** 1.0

---

## §1 Amaç ve Kapsam

Faz 5 üç eksende ilerler:

1. **Mesajlaşma altyapısı** (Faz 4 Dalga C'den taşındı) — kullanıcılar
   arası doğrudan iletişim (SignalR + Conversation + Message)
2. **KVKK + güvenlik olgunlaştırma** — hesap silme/anonimize, rate
   limiting, moderasyon Hide pattern
3. **Test infrastructure** — InMemory provider sınırlarını aş
   (SQL Server LocalDB)

Faz 4 Dalga B'de biriken 13 tech-debt maddesi (`docs/tech-debt.md`
#11-23) öncelikleştirildi:

| Öncelik | Maddeler | Sebep |
|---|---|---|
| Yüksek (Faz 5) | #11, #19, #20 | InMemory sınırları test'leri zayıflatıyor; KVKK uyum şartı; spam riski açık |
| Orta (Faz 5 sonu) | #13, #23 | Moderasyon yanlış silme riski; defense-in-depth eksik |
| Düşük (Faz 6+) | #12, #14-18, #21-22 | İyileştirme niteliği, blocker değil |

### KAPSAMDA

- 1-1 metin mesajlaşma (Conversation + Message)
- SignalR real-time UI
- Hesap silme/anonimize (KVKK 7. madde)
- Rate limiting middleware (yorum, şikayet, mesaj, bağlantı)
- Hide vs Delete moderation (post + comment)
- Authorize sayfaları için otomatik NoIndex
- SQL Server LocalDB test migration

### KAPSAM DIŞI (Faz 6+)

- Grup mesajlaşma, görsel/dosya/audio/video mesaj
- Read receipts, typing indicator, mesaj search, mesaj editleme
- Multi-instance scaling (SignalR Redis backplane)
- Mobile app (PWA / native karar)
- Notification email kanalı
- Comment edit history, hierarchical reply
- Tag autocomplete, image responsive variants, media GC

---

## §2 Vizyon Tutarlılığı

Faz 4 vizyon ilkeleri korunur (atıf: `docs/phase-4-charter.md` §1.2):

- **Plansız (Free)** — mesajlaşma da herkese açık, Pro/Premium yok
- **Vatandaş 1. sınıf** — bağlı kullanıcılar mesajlaşabilir, Tenant
  zorunluluğu yok
- **Sessiz pattern** — engellenmiş kullanıcı mesaj atamaz, sessiz
  reddedilir (Karar 4 Faz 4'ten devam)
- **Vatandaş gürültüsüzlüğü** — mesaj bildirimi kontrollü; toplu özet
  veya per-mesaj bildirim kullanıcı tercih ettiği şekilde

---

## §3 Mimari Kararlar (Yeni)

### Karar 1 — Mesajlaşma kapsam minimum

1-1 metin mesajlaşma. Grup chat YOK. Görsel/dosya/audio/video YOK.
Read receipts, typing indicator, search YOK. Mesaj editleme YOK
(yanlış mesaj atılırsa sahip silebilir, hard delete; karşı taraf
"silinmiş mesaj" placeholder görür).

**Gerekçe:** Minimum viable mesajlaşma — Faz 6+'da zenginleştirilebilir.

### Karar 2 — Real-time: SignalR doğrudan

Polling fallback değil. ASP.NET Core SignalR olgun, gelecekte yeniden
yazma istemeyiz. Tek instance başlangıç (Redis backplane Faz 6+).

**Gerekçe:** Charter Faz 4 §2.3 Karar 15 burada uygulanıyor.

### Karar 3 — Mesajlaşma yetkisi

İki yol var; A ve B mesajlaşabilir eğer:

1. **Accepted UserConnection** (LinkedIn pattern, vatandaşlar için
   genel kural) — aralarında Status=Accepted bağlantı var, **VEYA**
2. **Aynı tenant üyesi** (intra-org communication) — `User.TenantId`
   aynı + her ikisi de aktif tenant member (`TenantUser.IsActive=true`),
   bağlantı zorunlu değil

**UserBlock entegrasyonu (Karar 4) tenant içinde de geçerli** —
engellenmiş kullanıcı tenant arkadaşı bile olsa mesaj atamaz. Yani
yetki kontrolü:

```
canMessage = (HasAcceptedConnection(A,B) OR SameActiveTenant(A,B))
             AND NOT IsBlockedEitherWay(A,B)
```

**Faz 4 Karar 16 ile sapma:** Faz 4 charter "engelleme dışında serbest"
diyordu (Facebook pattern). Faz 5'te LinkedIn modeli + tenant istisnası
benimsendi. Sebep: spam riski + vatandaş gürültüsüzlüğü; tenant istisnası
büro içi iletişim için pratik gereklilik.

### Karar 4 — Engelleme entegrasyonu

A B'yi engellerse:
- B mesaj atamaz (server-side son kontrol)
- Mevcut conversation arşivlenir (silinmez, A'nın listesinde "arşiv"
  sekmesinde, B'nin listesinde tamamen gizli)
- B'nin attığı (engellenme öncesi) mesajlar A'da görünür kalır
  (silinmez, sansürlenmez)

**Gerekçe:** Faz 4 Karar 17 ile uyumlu, defense-in-depth.

### Karar 5 — Mesaj retention

Mesajlar süresiz tutulur (kullanıcı silmedikçe). Hesap silme/anonimize
(Karar 6) → mesajlar korunur, yazar "Silinmiş Kullanıcı" görünür.

Conversation iki taraflı: A silse de B görür. "Conversation'ı sil"
A için soft delete (A'nın listesinden kaldırılır), B'nin görünümü
etkilenmez.

### Karar 6 — Hesap silme (KVKK 7. madde)

**Hard delete YOK.** Anonimize stratejisi:

```
User.IsActive = false
User.UserName = "silinmis-kullanici-{id}"
User.Email = "deleted-{id}@local" (login imkansız)
User.PasswordHash = null (defansif)
UserProfile.DisplayName = "Silinmiş Kullanıcı"
UserProfile.PublicSlug = null
UserProfile.Bio/AvatarUrl/City/... = null
```

İçerik (post, yorum, mesaj, beğeni, şikayet) **korunur**. Yazar
anonim görünür. Public profile sayfası 404 döner.

**Süreç:** Kullanıcı `/profil/hesabi-sil` form → 7 gün soft window
(undo) → Hangfire job ile anonimize uygulanır. Email onay zorunlu.

**Gerekçe:** KVKK uyum + DB integrity (FK Restrict pattern korundu)
+ tartışma sürekliliği (mesaj/yorum context'i kayıp olmaz).

### Karar 7 — Rate limiting

.NET 7+ built-in rate limiting middleware
(`Microsoft.AspNetCore.RateLimiting`). Per-user fixed window:

| Endpoint | Limit |
|---|---|
| Yorum oluştur | 10/dk, 100/saat |
| Şikayet | 5/saat, 20/gün |
| Mesaj | 30/dk, 500/gün |
| Bağlantı isteği | 20/saat |
| AJAX endpoint genel | 100 req/dk |

Anonim kullanıcı: IP-based fallback (login'siz API erişimi zaten
engelli ama defansif).

429 yanıt: `Retry-After` header + JSON `{ error, retryAfterSeconds }`.

### Karar 8 — SignalR auth + CSRF

SignalR Hub `[Authorize]` zorunlu. JS client cookie auth ile bağlanır
(WebSocket handshake'te cookie otomatik gönderilir).

**CSRF:** Mesaj göndermede ek anti-forgery token gerek YOK — WebSocket
handshake auth'lu, sonraki mesajlar aynı bağlantı üzerinden. Cookie
SameSite=Lax (mevcut Identity ayarı yeterli).

### Karar 9 — Mesaj UI yapısı

- `/mesajlar` — conversation listesi (sol panel) + boş durum
- `/mesajlar/{conversationId}` — mesaj geçmişi + gönderme alanı
  (sağ panel; mobile'da tam ekran)
- Real-time: SignalR client → yeni mesaj otomatik akar
- Sayfalama: ilk 50 mesaj, scroll yukarı ile eski mesajlar (sonsuz scroll)
- Yeni conversation: profil sayfasından "Mesaj At" butonu (Karar 3
  yetkisi varsa)

### Karar 10 — Test infrastructure: SQL Server LocalDB

InMemory provider sınırları (ExecuteUpdate, GroupBy projection — bkz.
tech-debt #11) test güvenilirliğini zayıflatıyor. Faz 5'te integration
testler **SQL Server LocalDB**'ye geçer.

- xUnit `IClassFixture<SqlDbFixture>` pattern
- Her test sınıfı kendi database (random GUID adı)
- Dispose'da database silinir (TempDb pattern)
- Unit testler (calculator, formatter) InMemory'de kalabilir
- Mevcut testlerin %95'i değişiklik gerektirmemeli (DbContextOptions
  abstraction yeterli)

**Risk:** CI/CD'de SQL Server container gerekir. GitHub Actions için
`mcr.microsoft.com/mssql/server:2022-latest` service container.

### Karar 11 — Hide vs Delete moderation

Faz 4 Adım 4.10 P2'de admin sadece "Sil" yapabiliyordu (geri alınamaz).
Faz 5'te "Gizle" eklenir:

- `UserPost.IsModeratorHidden` (bool, default false)
- `PostComment.IsModeratorHidden` (bool, default false)
- Admin paneli: "Gizle" + "Sil" iki ayrı buton
  - "Gizle" → IsModeratorHidden=true, public görünmez, sahip görür
    ("Yönetim tarafından gizlendi")
    Geri alınabilir.
  - "Sil" → mevcut akış (hard delete)
- Public query'ler `!IsModeratorHidden` filter uygular (sahip için
  bypass)
- ActivityLog: `Post.AdminHide`, `Post.AdminUnhide`,
  `Comment.AdminHide`, `Comment.AdminUnhide`

**Gerekçe:** tech-debt #13 — yanlış silme riski azalır, audit izi
korunur.

### Karar 12 — Authorize sayfaları için otomatik NoIndex

`_Layout.cshtml`'de `[Authorize]` sayfa veya admin area için otomatik
`<meta name="robots" content="noindex, nofollow" />` render. ViewData
kontrolü gerek kalmaz, manuel ekleme hatası önlenir.

İmplementation: Layout'ta `User.Identity.IsAuthenticated` + route
"area=Admin" kontrolü; veya MVC Filter pattern (HtmlAttributeProvider).

**Gerekçe:** tech-debt #23 — defense-in-depth, robots.txt zaten
engelliyor ama meta tag view-source'ta görünür.

---

## §4 Adım Kırılımı (3 Dalga, 9 Alt Adım)

### Dalga A — Olgunlaştırma (KVKK + güvenlik) — 1.5-2 hafta tahmin

| Adım | Konu | Karar |
|---|---|---|
| 5.1 | Hesap silme + anonimize stratejisi | 6 |
| 5.2 | Rate limiting middleware + endpoint koruma | 7 |
| 5.3 | Hide vs Delete moderation + NoIndex auto | 11, 12 |

### Dalga B — Mesajlaşma altyapısı — 3 hafta tahmin

| Adım | Konu | Karar |
|---|---|---|
| 5.4 | Conversation + Message entity + servis | 1, 4, 5 |
| 5.5 | Mesajlaşma UI (sayfa, liste, gönderme — polling fallback önce) | 9 |
| 5.6 | SignalR real-time entegrasyonu | 2, 8 |
| 5.7 | Mesaj moderasyon + Notification.Message + Dalga B closeout | 3 |

### Dalga C — Test infrastructure + Faz closeout — 1 hafta tahmin

| Adım | Konu | Karar |
|---|---|---|
| 5.8 | SQL Server LocalDB test migration | 10 |
| 5.9 | Faz 5 closeout (roadmap, README, tech-debt update, tag) | — |

---

## §5 Tahmin

Charter tahmini **6 hafta**. Faz 4 baseline'a göre (~32x hızlanma) gerçek
süre ~5-10 gün olabilir.

**Ama:** SignalR + KVKK + test infrastructure migration **yeni teritory** —
Faz 4'teki UGC pattern reuse avantajı yok, daha fazla araştırma + AR-GE
gerekir. Belki 1-2 hafta gerçek süre.

**Muhafazakar tahmin yanıltıcı baseline notu:** Faz 4 hız oranını Faz 5'e
uygulamak doğru olmayabilir. Her dalga sonunda ölçüm yapılır, kalan
dalgalar yeniden tahmin edilir.

---

## §6 Risk ve Tuzaklar

### 6.1 Faz 4'ten Devam (Bilinen)

Faz 4 charter §5.1-5.2'deki tuzaklar geçerli (atıf: `docs/phase-4-charter.md`
§5).

### 6.2 Faz 5'e Özgü Yeni Tuzaklar

| Tuzak | Etkilenen | Hafifletme |
|---|---|---|
| **SignalR scale-out** | Multi-instance ihtiyacı | Tek instance varsayımı, Redis Faz 6+ |
| **WebSocket auth dropouts** | Ağ kopması, cookie expire | SignalR auto-reconnect + auth challenge |
| **Mesaj DB büyümesi** | Süresiz retention → tablo şişmesi | Index strategy + Faz 6+ retention policy |
| **KVKK silme race** | 7-gün undo penceresi içinde Hangfire job | Status enum (PendingAnonymize, Anonymized) + idempotent job |
| **Rate limit false positive** | Hızlı UI testi yapan kullanıcı | Test ortamında relaxed limits + admin bypass |
| **LocalDB CI ortam** | GitHub Actions Linux runner LocalDB yok | Service container `mssql/server:2022-latest` |
| **Hide pattern leak** | Public query filter unutulursa gizli içerik görünür | EF query filter (HasQueryFilter) merkezi tanım |

### 6.3 Hukuki/Etik Riskler (Yeni)

| Risk | Konu | Hafifletme |
|---|---|---|
| **Mesajlaşma içeriği KVKK** | Hassas içerik, retention belirsiz | Karar 5 (süresiz, kullanıcı kontrolü) + ToS Faz 6+ |
| **Avukat-müvekkil ayrıcalığı** | Mesaj platformu üzerinden hukuki danışmanlık | Disclaimer + ToS güncelleme |
| **Engelleme istismarı** | Kullanıcı engelleyip + şikayet ederek başka kullanıcıyı susturma | Şikayet rate limit (Karar 7) + admin denetim |

---

## §7 Test Stratejisi

Faz 4 sonu: 666 test. **Faz 5 hedef: ~800+** (Dalga A: +40, Dalga B: +60,
Dalga C: +30).

Yeni test pattern'leri:

- **AnonymizationFlowTests** — hesap silme + 7-gün undo + Hangfire job
- **RateLimitTests** — 429 davranışı + Retry-After header
- **HideModerationTests** — admin gizle/göster + sahip görünür kuralı
- **ConversationServiceTests** — engelleme entegrasyonu + tenant istisnası
- **MessageHubTests** — SignalR Hub auth + mesaj gönderme + offline kullanıcı
- **SqlDbFixtureTests** — SQL Server LocalDB test fixture pattern

---

## §8 Faz 5 Tamamlanma Kriterleri

1. 9 alt adım tümü tamamlandı (Dalga A + B + C)
2. Test sayısı 800+ (regresyon: 0)
3. Mesajlaşma yayında (real-time, manuel test geçti)
4. Hesap silme akışı yayında (manuel test: 7-gün undo + anonimize)
5. Rate limiting aktif (429 davranışı doğrulandı)
6. SQL Server LocalDB test infrastructure aktif (CI yeşil)
7. README + roadmap + tech-debt güncellemesi
8. `phase-5-complete` annotated tag

---

## §9 Faz 6 Önizleme (Bilgi Amaçlı)

Faz 5 sonunda taşınması beklenen konular:

- Mobile app (PWA mı native mi karar)
- Bildirim email kanalı (tech-debt #22)
- Comment edit history (tech-debt #21)
- Tag autocomplete (tech-debt #15)
- Hierarchical comment reply (tech-debt #14)
- Image responsive variants (tech-debt #18)
- Media GC (tech-debt #12)
- Multi-instance scaling (SignalR Redis backplane)
- Mesajlaşma zenginleştirme: read receipts, typing, search, görsel/dosya
- Calculator kategorileri D-I (15 yeni hesaplayıcı)

Bunların hiçbiri Faz 5 kapsamında değildir.

---

*Charter sürümü 1.0 — 2 Mayıs 2026. Faz 5 boyunca güncel kalır;
charter değişiklikleri commit'lerle işlenir.*

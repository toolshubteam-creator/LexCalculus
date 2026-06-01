# Lex Calculus — Teknik Borç Defteri

Bu dosya, hızlı ilerlemek için bilinçli olarak ertelenen veya ileride
gözden geçirilmesi gereken teknik kararları kayıt altına alır. Her madde
kalıcı bir bug değil; "şu an yeterli, ama bir gün dokunulmalı" niteliğinde.

Faz sonlarında bu listeyi gözden geçir; uygun olanları çöz, hâlâ erken
olanları ertele.

---

## 1. Hangfire bağımlılığı Infrastructure katmanına sızdı

**Bağlam:** Adım 3.3 Parça 5/6 (admin dashboard widget'ları). Hangfire
recurring job sayısı ve aktif server sayısını widget'ta göstermek için
`JobStorage.Current` static API'sine ihtiyaç doğdu.

**Mevcut durum:** `LexCalculus.Infrastructure.csproj` artık `Hangfire.Core`
NuGet paketine bağımlı. Faz 1 mimarisinde Hangfire sadece `Web` ve `Jobs`
katmanlarındaydı (`README.md` "Background Jobs: Hangfire" kısmı `Jobs`
projesini ima ediyordu).

**İdeal çözüm:** `IHangfireSummaryProvider` arayüzü `Core`'a tanımla.
Implementation `Web` veya `Jobs`'a yerleştir (Hangfire zaten orada).
`DashboardSummaryService` bu arayüzü inject etsin, `JobStorage.Current`
çağrısını bilmesin.

**Önerilen zaman:** Faz 4'te yapısal refactor yapılırsa veya bir başka
servisin Hangfire'a Infrastructure'dan erişme ihtiyacı doğarsa. Şu an
maliyet/değer dengesi düşük: tek bir static çağrı için adapter pattern
overkill.

---

## ✅ 2. Seeder soft-delete restore mantığı (Bulgu 1, Adım 3.2 E2E) — Kapatıldı (2026-04-30, Adım 3.9 P1/2)

**Bağlam:** Adım 3.2 Parça 3 E2E cache invalidation testinde keşfedildi.
Admin paneli soft-delete yapabildiği için, kanonik bir seed satırı
(örn. kıdem tavanı 2024-01-01) yanlışlıkla silinirse `CalculatorParameterSeeder`
restart sonrası restore edemiyor.

**Mevcut durum:** Seeder'ın `AnyAsync` pre-check'i global query filter'a
uyduğu için soft-deleted satırı görmüyor → `INSERT` deniyor → `(ToolSlug, Key,
EffectiveDate)` unique constraint patlıyor → uygulama boot fail.

**İdeal çözüm (B yolu, Esat onayı 28.04):** Seeder pre-check
`IgnoreQueryFilters()` kullansın. Eğer kanonik satır soft-deleted bulunursa
`IsDeleted=false` yaparak restore etsin. Admin'in eklediği yeni (seed'de
olmayan) satırlar etkilenmez.

**Önerilen zaman:** Adım 3.9 (Faz 3 final temizlik). Bu kavramsal değişiklik:
seeder bundan sonra "satır yoksa ekle" değil, "kanonik satır mevcut formdaysa
garanti et" olacak.

**Çözüm:** `CalculatorParameterSeeder.SeedAsync` artık `IgnoreQueryFilters()`
ile mevcut satırı sorguluyor. Bulunursa: `IsDeleted=true` ise restore eder
(IsDeleted=false), aksi halde skip. Bulunmazsa Insert eder. Boot fail
riski ortadan kalktı. 2 yeni test eklendi (idempotent re-run, restore).

---

## 3. EF Core migration default değer dikkati

**Bağlam:** Adım 3.3 Parça 4a — `ApplicationUser.NotificationsEmailEnabled`
alanı eklendi. C# initializer'da `= true` olmasına rağmen EF Core migration
otomatik olarak `defaultValue: false` üretti. Mevcut kullanıcılar için
backfill yapılmadı; manuel SQL UPDATE ile düzeltildi.

**Mevcut durum:** Sapma manuel düzeltildi (commit 5ac5bb3). Ama bu, gelecek
migration'lar için sessiz bir tuzak: yeni nullable olmayan default değerli
alan eklerken her zaman migration dosyasını gözden geçirmek gerekiyor.

**İdeal çözüm:** İki seçenek:
1. Her migration'da Up() metodunu manuel inceleme alışkanlığı (insan-süreci)
2. Custom MigrationOperationGenerator yazmak — entity property'sindeki
   initializer değerini DB default'una otomatik çevirir (kod-süreci)

Şu an seçenek 1 yeterli. Faz 3 boyunca 3 migration var, hepsi gözden geçirildi.

**Önerilen zaman:** Eğer Faz 4'te 5+ yeni migration olur ve aynı sorun 2 kez
daha çıkarsa, seçenek 2'ye geç. Şimdilik bu dosyada uyarı olarak kal.

---

## 4. Hangfire Dashboard authorization: 401 vs 403

**Bağlam:** Adım 3.3 Parça 1/6 (Hangfire dashboard auth). Anonim kullanıcılar
`/admin/hangfire`'a girerken `IDashboardAuthorizationFilter.Authorize` false
döndürüyor → Hangfire 401 atıyor.

**Mevcut durum:** Anonim → 401 (Unauthorized). Beklentimiz cookie auth'un
`/Identity/Account/Login`'e redirect etmesiydi (302). Hangfire kendi auth
katmanı sebebiyle bunu yapmıyor.

**Ek senaryo:** Authenticated non-admin user (örn. Adım 3.6'da kurulacak
"Avukat" rolü) bu endpoint'i denerse muhtemelen yine 401 alacak — doğrusu
403 (Forbidden) olmalıydı.

**İdeal çözüm:** `AdminDashboardAuthorizationFilter`'ı genişlet:
- User authenticated değilse → 401 (mevcut davranış doğru)
- User authenticated ama Admin değilse → 403 (yeni)

Hangfire'ın filter API'si bunu doğal desteklemiyor; muhtemelen middleware
tarafında pre-check + Hangfire dashboard'a girmeden önce manuel redirect
gerekiyor.

**Önerilen zaman:** Adım 3.6 (Rol Yönetimi) — gerçek senaryo (Avukat rolünde
kullanıcı dashboard'a girmeye kalkar) o zaman test edilebilir hale gelecek.
Şimdi sadece Admin rolü olduğu için bu sorun teorik.

---

## ✅ 5. Register `SignInAsync` defensive try/catch — Kapatıldı (2026-04-30, Adım 3.9 P1/2)

**Bağlam:** Adım 3.6 Parça 2b-ii — Register.cshtml.cs OnPostAsync sonunda
`_signInManager.SignInAsync(user, isPersistent: false)` çağrısı try/catch
ile sarıldı. Sebep: test ortamında TestAuthHandler `IAuthenticationSignInHandler`
interface'ini implement etmediği için exception fırlatıyor; production'da
gerçek cookie auth handler exception atmaz, ama bu defensive kod runtime
hatalarını da sessizce yutar.

**Mevcut durum:** try/catch ile exception swallow + log. Production'da
bilinen bir runtime hatası yok, bu yüzden etkisi sıfır. Test'te akış
tamamlanıyor.

**İdeal çözüm:** İki yol var:
1. TestAuthHandler'ı `IAuthenticationSignInHandler` implement edecek
   şekilde genişlet (test fixture'a ait, production etkilenmez)
2. try/catch'i kaldır, gerçek exception olursa kullanıcıya "Hesap
   oluşturuldu, lütfen giriş yapın" mesajı gösterip Login sayfasına
   yönlendir

Yaklaşım 1 daha temiz (test ortamı production gibi davranır). Yaklaşım
2 production resilience artırır.

**Önerilen zaman:** Adım 3.9 (Faz 3 final temizlik). O noktada Parça 2b-iii
ile Identity akışı tamamen oturmuş olur, yaklaşımı değerlendirmek için
bağlam tam olur.

**Çözüm:** Seçenek 1. `TestAuthHandler` artık `IAuthenticationSignInHandler`
implement ediyor (`SignInAsync`/`SignOutAsync` no-op). Hem `Register.cshtml.cs`
hem `Manage/Index.cshtml.cs` (RefreshSignInAsync) try/catch'leri kaldırıldı.
Production kod sade; test ortamı production gibi davranıyor.

---

## ✅ 6. ChangePassword sayfası scaffold edilmedi — Kapatıldı (2026-04-30, Adım 3.9 P1/2)

**Bağlam:** Adım 3.6 Parça 3/4 — profil sayfası `/profil`'de "Şifre
değiştir" butonu yer aldı, ancak `Manage/ChangePassword.cshtml` Faz 1
Identity scaffold'unda yoktu. Parça 3/4 kapsamı dışına çıkmamak için
bu sayfa eklenmedi; profil sayfasında "Şifre değiştir" disabled
placeholder olarak gösteriliyor.

**Mevcut durum:** Kullanıcı şifresini değiştiremiyor. "Şifremi
unuttum" ile reset linki üzerinden değiştirebiliyor (ForgotPassword
mevcut), ama bu yetkilendirilmiş kullanıcının kendi şifresini
değiştirmek için fazla dolaylı.

**İdeal çözüm:** `dotnet aspnet-codegenerator identity` ile
Manage/ChangePassword scaffold + Lex Calculus tema uygulaması. Yaklaşık
1 saatlik mini-spec.

**Önerilen zaman:** Adım 3.6 Parça 4/4 sonrası mini ek spec, veya
Adım 3.9 (Faz 3 final temizlik). Faz 3 boyunca admin manuel şifre
reset gönderebileceği için (Parça 4/4) acil değil.

**Çözüm:** `Manage/ChangePassword.cshtml(.cs)` Lex Calculus tema (form-section
+ field BEM) ile eklendi. `Manage/Index.cshtml`'deki disabled placeholder
gerçek link oldu. 3 yeni test eklendi (GET 200, POST happy path, POST yanlış
eski şifre validation).

---

## ✅ 7. Manage/_ManageNav.cshtml Bootstrap kalıntısı — Kapatıldı (2026-04-30, Adım 3.9 P1/2)

**Bağlam:** Adım 3.6 Parça 3/4 keşfi — Identity scaffold'undan gelen
`_ManageNav.cshtml` dosyası Bootstrap `nav-pills` class'ları kullanıyor
ve şu anda hiçbir sayfada include edilmiyor.

**Mevcut durum:** Dosya repo'da var ama kullanılmıyor. Dead code +
Bootstrap bağımlılığı sinyali.

**İdeal çözüm:** Ya silinir (kullanılmadığına göre), ya da Lex
Calculus tema diline çevrilir (`.admin-nav-link` benzeri pattern).
Eğer Faz 4'te Manage altında çoklu sayfa olursa nav lazım olabilir.

**Önerilen zaman:** Adım 3.9 (Faz 3 final temizlik). Şu an 0 etki
ama tarama yapılırken "Bootstrap kullanıyoruz mu?" yanlış sinyali
verir.

**Çözüm:** Dead code yolunu seçtik — `Manage/_ManageNav.cshtml` ve
`Manage/ManageNavPages.cs` silindi. Manage sayfaları zaten standalone
form-section pattern'i kullanıyordu, nav include'a ihtiyaç yok.
Faz 4'te Manage altında çoklu sayfa gerekirse temadan sıfırdan eklenir.

---

## ✅ 8. Views/TenantYonetim/Index.cshtml inline style ihlali — Kapatıldı (2026-04-30, Adım 3.9 P1/2)

**Bağlam:** Adım 3.7 P3/5 commit'inde TenantYonetim view'ı yazılırken
hızlı çıktı için inline style'lar kullanıldı (`style="margin-bottom:20px"`,
`style="font-size:18px"`, `style="display:inline"`). CLAUDE.md kuralı
"Inline style yasak; tüm CSS wwwroot/css/ altında BEM ile" diyor.

**Mevcut durum:** UI çalışıyor. Sadece konvansiyon ihlali; e-mail
template istisnası değil.

**İdeal çözüm:** Inline style'ları `forms.css` veya yeni bir
`tenant-yonetim.css`'e taşı, BEM class adlarına çevir. P4/5'teki
"Tenant'tan Ayrıl" butonu için kullanılan `.tenant-leave` ve
`.tenant-leave__btn` BEM örnek olarak hazır — diğer inline'lar da
benzer şekilde çevrilmeli.

**Önerilen zaman:** Adım 3.9 cleanup turu. Tahmini iş 15 dakika.

**Çözüm:** 6 inline style temizlendi. `forms.css`'e `tenant-yonetim__meta`,
`tenant-yonetim__section-title` (+ `--wide` modifier), `tenant-yonetim__inline-form`
BEM class'ları eklendi. View artık hiç inline style içermiyor.

---

## 9. ActivityLog retention policy belirsiz (Adım 3.8 P1/2)

**Bağlam:** Adım 3.8 P1/2'de eklendi. ActivityLog tablosu KVKK kapsamında
kişisel veri içeriyor: UserId, IpAddress, UserAgent, MetadataJson.
Şu anda sınırsız tutuluyor (silme yok).

**Mevcut durum:** Yazılan tüm kayıtlar süresiz birikiyor. Tablo zamanla
sürekli büyüyecek; KVKK bakış açısıyla "süresiz tutum" gerekçesiz.

**İdeal çözüm:** Avukat/danışman görüşü alınıp net bir retention süresi
belirlenmeli (yaygın aralık: 2-5 yıl). Karar sonrası Hangfire recurring
job ile cron temizleme: günlük bir kez, `CreatedAt < (UtcNow - retention)`
olan kayıtları sil.

**Önerilen zaman:** Hukuki görüş + 1 saat job kodu. Adım 3.8 P2/2 sonrası
veya Faz 4 başında.

---

## Bu dosya nasıl güncellenir?

Yeni bir tech debt maddesi ortaya çıktığında:
1. Bu dosyaya 4 başlıklı format ile ekle (Bağlam → Mevcut → İdeal → Zaman)
2. Commit mesajında "docs(tech-debt): X eklendi" formatı kullan
3. Faz sonu retrospektifte ödün düş — "henüz mi, şimdi mi"

Çözülen maddeler için: silmek yerine "ÇÖZÜLDÜ (commit hash, tarih)"
notu ekle ve dosyanın altına taşı. Tarihçe değerli.

---

## Faz 4 Başlangıç Notu (30 Nisan 2026)

Faz 4 sırasında ortaya çıkması beklenen yeni tech-debt adayları (charter §7):

- **SignalR Redis backplane** — multi-instance scale-out ihtiyacı doğunca
- **Azure Blob storage geçişi** — yerel disk → cloud (görseller hacmi büyüyünce)
- **Mesaj retention policy** — KVKK kapsamında, avukat görüşü gerekir
- **Görsel CDN** — yerel servis yetersiz kalınca (Cloudflare R2, Bunny vb.)
- **Elasticsearch full-text search** — DB `LIKE` yetersiz kalınca (post/comment arama)
- **Hreflang (TR/EN multi-locale)** — uluslararası görünürlük gerektiğinde

Bu maddeler Faz 4 boyunca eklenir, kapatılmazsa Faz 5'e devredilir.

---

## 10. Admin slug yönetimi (Faz 5+)

**Bağlam:** Adım 4.1 P2/3 sonrası tasarım kararı (30 Nisan 2026). Public slug
"görünmez kimlik" olarak yeniden tasarlandı (Yaklaşım 4): kayıt anında
DisplayName'den otomatik üretilir, kullanıcı UI'da görmez/değiştirmez.

**Konu:** Slug ilk üretimde sabitlenir, kullanıcı değiştiremez. Edge case'ler:
- Kullanıcı DisplayName değişikliği sonrası slug tutarsızlığı (Av. unvanı,
  soyadı değişikliği)
- Kullanıcı kendi slug'ından memnun olmayabilir (örn. otomatik "uye-99"
  fallback, yanlış slugify, vb.)
- SEO açısından slug değişikliği eski URL'leri kırar (redirect mapping ihtiyacı)

**Risk:** Düşük — şu an kullanıcı talebi yok, teorik. Faz 4 yayını sonrası
gerçek kullanıcı geri bildirimine göre öncelik belirlenir.

**Çözüm önerisi:** Faz 5+'ta admin paneline "kullanıcı slug düzenle":
- `Areas/Admin/Controllers/UsersController` action: `PublicSlugDuzenle`
- Gerekçe alanı zorunlu (audit log metadata)
- ActivityLog entegrasyonu (`User.SlugChange` action, eski → yeni)
- Opsiyonel: eski slug → yeni slug redirect mapping (`SlugRedirect` entity
  + middleware) — SEO ve dış linkler için

**Tahmini iş:** ~1 gün (controller + view + audit + migration; redirect
mapping eklemek 1 gün daha).

**Önceliklendirme:** Faz 4 yayını sonrası kullanıcı geri bildirimine göre.

---

# Faz 4 Dalga B Sırasında Ortaya Çıkan/Ertelenen (2 Mayıs 2026)

Adım 4.5 - 4.10 sırasında bilinçli olarak ertelenen veya keşfedilen
maddeler. Hepsi Faz 5+ aday — UGC katmanı yayında çalışıyor, bunlar
"ileride iyileştirme" niteliğinde.

---

## ✅ 11. EF Core InMemory provider sınırları (test infrastructure) — Kapatıldı (2026-05-15, Adım 5.8 P2)

**Çözüm:** Tüm test altyapısı SQL Server LocalDB'ye geçirildi (Seçenek 1).
`SqlServerTestDb` (per-test fresh DB, IAsyncLifetime via `SqlServerTestBase`),
`SqlServerTestAuthWebApplicationFactory`, `SqlServerWebApplicationFactoryFixture`
ile InMemory provider tamamen kaldırıldı. `Microsoft.EntityFrameworkCore.InMemory`
NuGet referansı çıkarıldı. Tüm 779 test gerçek SQL Server semantiği ile koşuyor
(IDENTITY, FK enforcement, ExecuteUpdate, GroupBy translation, transaction).
P1 + P2 toplam: 162 kırığın hepsi düzeltildi, yeşil. Detay: [[#34]].

---

## 11. (Orijinal kayıt — referans için)

### EF Core InMemory provider sınırları (test infrastructure)

**Bağlam:** Adım 4.5 (PostTag UsageCount), 4.7 (UserPost ViewCount),
4.9 (PostLike toggle), 4.10 P1 (ContentReportGroup) — atomik update veya
kompleks GroupBy projection istendiğinde InMemory provider'ın eksik
desteği fark edildi.

**Mevcut durum:** EF Core 10 InMemory provider şu API'leri desteklemiyor:
- `ExecuteUpdateAsync` / `ExecuteDeleteAsync`
- `GroupBy` + projection ile `ToDictionaryAsync` (bazı kombinasyonlar)

Etki: tracked entity + SaveChanges fallback'i kullanıldı. Semantik
doğru, perf etkisi tek-satır seviyesinde küçük. Production SQL Server
ExecuteUpdate kullanır.

**İdeal çözüm:** Test altyapısını SQL Server LocalDB veya Testcontainers
(gerçek SQL Server) ile besleyip InMemory'den ayrılmak. Yaklaşım seçenekleri:
- **Seçenek 1:** Tüm integration testleri LocalDB'ye geçir (CI'da Docker
  SQL Server container)
- **Seçenek 2:** Sadece atomik update gerektiren testleri ayır
  (`[Trait("Provider","Sql")]`), pipeline iki etap

**Önerilen zaman:** Faz 5 başında — yeni atomik update senaryoları
(Faz 5 KVKK silme, Faz 5 advanced UGC) testlerin doğruluğunu zayıflatır.

---

## 12. Media garbage collection (orphan upload temizleme)

**Bağlam:** Adım 4.8 (görsel altyapısı) ve 4.10 (içerik silme). UserPost
silindiğinde featured image dosyası diskte kalır; UserPost.Body içindeki
inline image'lar (`/uploads/posts/{userId}/{guid}.webp`) post silinince
disk üzerinde orphan kalır.

**Mevcut durum:** Disk şişmesi uzun vadede risk. Adım 4.10 P2'de
ContentReportService.ActionAsync da aynı: post sildi ama dosya kaldı.
PostComment'te embed image yok (sadece text + auto-link), o yüzden
yorum silmede sorun yok.

**İdeal çözüm:** Hangfire recurring job (`MediaGcJob`):
1. DB'deki tüm UserPost.Body + UserPost.FeaturedImageUrl'lerden img src çıkar
2. Avatar URL'leri (UserProfile.AvatarUrl) çıkar
3. Disk'i tara (`wwwroot/uploads/posts/`, `wwwroot/uploads/avatars/`)
4. Disk'te olup DB'de referans verilmemiş, 30+ gün önce yüklenmiş
   dosyaları sil
5. ActivityLog: `MediaGc.Run` (silinen dosya sayısı + toplam byte)

**Önerilen zaman:** Faz 5 — disk monitoring eşiği koyup gerçek
şişme görülünce. Tahmini iş: ~1 gün.

---

## ✅ 13. Hide vs Delete moderation (ContentReportService) — ÇÖZÜLDÜ (Adım 5.3 + 5.7, 15 Mayıs 2026)

**Çözüm:** `UserPost.IsModeratorHidden` ve `PostComment.IsModeratorHidden`
flag'leri Adım 5.3'te eklendi; `ContentReportService.HideAsync`/`UnhideAsync`
tarafından kullanılıyor. `Message.IsModeratorHidden` Adım 5.7'de eklendi
(`MessageService` admin Hide akışı). Hard delete `Action` butonu artık
opsiyonel — admin önce hide, son çare sil.

---

## 13. (Orijinal kayıt — referans için)

### Hide vs Delete moderation (ContentReportService)

**Bağlam:** Adım 4.10 P2'de ActionAsync sadece "Sil" yapabiliyor.
Geri alınamaz, yanlış silme riski. UserPostService.DeleteAsync ile
aynı API yüzeyi.

**Mevcut durum:** Admin "İçeriği Sil" butonu confirm dialog'u var ama
yanlış karar geri alınamaz. Sahip da bilgilendirildiği için itiraz
edemez (Notification gönderildi ama içerik gerçekten gitti).

**İdeal çözüm:** Hide pattern ekle:
- `UserPost.IsModeratorHidden` (bool, default false)
- `PostComment.IsModeratorHidden` (bool, default false)
- Public query'ler `IsModeratorHidden = false` filter'ı uygular
  (sahip için bypass — kendi içeriğini "moderasyona alındı" görür)
- Admin paneli "Gizle" + "Sil" iki ayrı buton; "Gizle" geri alınabilir
- ActivityLog: `Post.AdminHide`, `Post.AdminUnhide`

**Önerilen zaman:** Faz 5 — yanlış silme gerçekten yaşanırsa öncelik
artar. Tahmini iş: ~1-2 gün (entity + servis + view + test).

---

## 14. Hierarchical comment reply (PostComment.ParentCommentId)

**Bağlam:** Adım 4.9 P1/2 entity tasarımı sırasında karar verildi —
yorumlar düz, "yoruma yorum" yok. Charter §3.3'te ParentCommentId
nullable olarak listelenmişti ama scope minimize için eklenmedi.

**Mevcut durum:** Tartışmalar tek seviye. Birden fazla yorum aynı
makaleye gelirse hangi yorum hangine yanıt belli değil; mention
da yok.

**İdeal çözüm:** `PostComment.ParentCommentId` nullable + 1 seviye
nesting (Reddit-style sınırsız değil; LinkedIn pattern):
- Migration: kolon ekle, mevcut yorumlar null
- Servis: CreateAsync `parentCommentId` parametresi
- UI: "Yanıtla" butonu, yanıt formu nested
- NotificationType.CommentReply (zaten enum'da yok ama 4.9'da düşünülmüştü)

**Önerilen zaman:** Faz 5 advanced UGC. Kullanıcı talebine göre
öncelik. Tahmini iş: ~1-2 gün.

---

## ✅ 15. Tag autocomplete (kullanıcı yazımı esnasında) — ÇÖZÜLDÜ (Adım 6.6, 30 Mayıs 2026)

**Çözüm:** `IPostTagService.SearchByPrefixAsync` (StartsWith + UsageCount DESC +
Name ASC, min 2 karakter, take 1-20 clamp). `GET /api/post-tags/search` public
endpoint (AllowAnonymous + ajax-general rate limit). `wwwroot/js/tag-autocomplete.js`:
200 ms debounce, klavye erişilebilir (Arrow/Enter/Escape), mousedown seçim, dış-tık
kapatma; XSS güvenli (textContent). Mevcut chip mantığı `window.lexAddTag` ile reuse
(MAX_TAGS/duplicate/uzunluk korundu). Charter Karar 5. 5 test (search) + 1 (API).

**Bağlam:** Adım 4.6 P3'te tag chip vanilla JS, kullanıcı serbest yazıyor.
Mevcut popüler tag'ler önerilmiyor.

**Mevcut durum:** Kullanıcı "iş hukuku" yazar, başka kullanıcı "iş hukukuk"
typo yapar — iki ayrı tag oluşur. Slug normalize çakışmayı engelliyor
ama önerme yok.

**İdeal çözüm:**
- `IPostTagService.GetPopularAsync` zaten hazır
- Vanilla JS suggestions UI (datalist veya custom dropdown)
- Input change → debounced query → matching tag'ler dropdown
- Kullanıcı klavye veya tıklama ile seçer

**Önerilen zaman:** Faz 5 advanced UGC. Tahmini iş: ~0.5 gün
(servis hazır, UI önemli).

---

## ✅ 16. View count dedupe (bot/refresh shielding) — ÇÖZÜLDÜ (Adım 6.6, 30 Mayıs 2026)

**Çözüm:** `Makale.cshtml.cs` `RegisterUniqueView` (charter Karar 4). Anonim:
HttpOnly + IsEssential cookie `vc_{postId}` (30 dk, SameSite=Lax, HTTPS-aware Secure).
Login: `IMemoryCache` `vc_{userId}_{postId}` (30 dk SlidingExpiration). DB'ye ek tablo
YOK. Mevcut yayın/gizli/sahip gate korundu (dedupe ek katman). Increment servise
taşınmadı — Web-katmanı concern (cookie/HttpContext) sayfada kaldı, Infrastructure
sızıntısı önlendi. `AddMemoryCache()` eklendi. MakalePageTests: dedupe (aynı client +1),
iki client +2, login non-owner dedupe.

**Bağlam:** Adım 4.7'de her GET +1 ViewCount. Bot trafiği veya
kullanıcının F5 ile refresh etmesi sayacı şişirir.

**Mevcut durum:** ViewCount gerçekçi olmayan değerler alabilir.
Yazar UI'da ViewCount görüyorsa ("12 okuma") gerçek metriği yansıtmaz.

**İdeal çözüm seçenekleri:**
1. **Cookie-based dedupe (basit):** ViewCount artırmadan önce
   `viewed_post_{id}` cookie kontrolü, 30 dk TTL
2. **IP+UserAgent fingerprint (orta):** Hash + 24 saat dedupe
3. **robots.txt + UA filter (defansif):** Bilinen bot UA pattern'lerinde
   ViewCount artırma

Seçenek 1 + 3 kombinasyonu yeterli olur.

**Önerilen zaman:** Faz 5 — ViewCount UI'a gerçekten yansıyana kadar
(şu an entity field, yazar görmüyor). Tahmini iş: ~0.5 gün.

---

## 17. Tag UsageCount decrement helper extract (refactor)

**Bağlam:** Adım 4.10 P2'de `ContentReportService.ActionAsync`
`UserPostService.DeleteAsync` ile aynı tag UsageCount decrement mantığını
duplike içeriyor. DRY ihlali — ama bilinçli (admin context'inde
servis-arası bağımlılık eklemek istenmedi).

**Mevcut durum:** İki yerde benzer kod:
- `UserPostService.DeleteAsync`: `_tagService.DecrementUsageAsync` çağrıları
- `ContentReportService.ActionAsync`: inline `tag.UsageCount--`

**İdeal çözüm:** `IPostTagService.DecrementForPostAsync(UserPost post,
CancellationToken ct)` helper extract. İki çağıran da bunu kullanır.

**Önerilen zaman:** Üçüncü çağıran çıkınca (örn. Faz 5 KVKK
"hesabımı sil" akışında kullanıcının tüm post'larını silmek). Tahmini
iş: ~30 dk.

---

## ✅ 18. Image responsive variants (srcset) — ÇÖZÜLDÜ (Adım 6.8, 1 Haziran 2026)

**Çözüm:** `MediaUploadService.UploadInlineImageAsync` inline görsel
yüklerken ana WebP (≤1200, q85) yanında 480w ve 800w responsive variant
üretir (Lanczos3, upscale yok — orijinalden büyük genişlik atlanır).
Render anında `ImageVariantEnricher` (regex tabanlı, `Makale.cshtml.cs`
→ `BodyHtml`) yalnız `uploads/posts/{id}/inline/{guid}.webp` img'lerine
disk'te variant'ı VAR olanlar için `srcset` + `sizes` + `loading="lazy"`
ekler. Eski resimler / harici / featured görseller dokunulmaz (graceful
fallback: browser `src` kullanır). İdeal plandan sapma: `<picture>` yerine
sade `srcset` (yeterli), genişlikler 480/800 (600/1200 değil — okuma kolonu
~760px, 1200 zaten `src` fallback). Test: `ImageVariantTests` (4).

---

## 18. (Orijinal kayıt — referans için)

**Bağlam:** Adım 4.8'de görsel yükleme tek 1200x1200 sürüm üretiyor
(featured: 1200x630 OG, inline: 1200 max). Mobile cihazlarda gereksiz
büyük dosya indiriliyor.

**Mevcut durum:** Mobile cihazda da 1200px görsel iniyor. CDN yok,
sıkıştırma WebP olsa bile 100-200 KB. Sayfa hızı (LCP) etkili.

**İdeal çözüm:** ImageSharp ile multiple varyant:
- Mobile: 600px max
- Desktop: 1200px max
- HTML `<picture>` + `<source media>` + `srcset` ile responsive

**Önerilen zaman:** Faz 5 — Core Web Vitals optimize edilirken.
Tahmini iş: ~1 gün (servis + storage path naming + view helper).

---

## ✅ 19. Hesap silme + KVKK anonimize — ÇÖZÜLDÜ (Adım 5.1, 15 Mayıs 2026)

**Çözüm:** `UserAnonymizationService` Adım 5.1'de eklendi (`PasswordHash`=null,
`UserName`/`Email` anonymized form, profil temizlik, ilişkili veri korunur).
Admin paneli "Hesabı anonimize et" akışı + KVKK kullanıcıya kendi-kendine silme
butonu yayında. Hard delete YOK — FK Restrict + content owner referansları
korunarak yasal sorumluluk denetlenebilir.

---

## 19. (Orijinal kayıt — referans için)

### Hesap silme + KVKK anonimize

**Bağlam:** Adım 4.6-4.10 boyunca biriken FK yapısı: ApplicationUser →
UserPost, PostComment, PostLike, ContentReport (Reporter +
ReviewedBy), UserConnection, UserBlock — tüm FK'ler User Restrict.
Hesap silme imkansız (DB integrity).

**Mevcut durum:** Kullanıcı "hesabımı sil" diyemez. KVKK 7. madde
gereği unutulma hakkı var ama mimari engel oluyor.

**İdeal çözüm:** Anonimize stratejisi (soft delete):
- `User.IsActive = false`
- `User.UserName = "silinmis-kullanici-{id}"`
- `User.Email = null` (veya `deleted-{id}@local`)
- `UserProfile.DisplayName = "Silinmiş Kullanıcı"`
- `UserProfile.PublicSlug = null`
- `UserProfile.Bio/AvatarUrl/...` temizle
- İçerik (post, yorum) korunur ama yazar "Silinmiş Kullanıcı" görünür
- Public profile sayfası 404 döner
- ActivityLog: `User.Anonymize` (audit + integrity için kayıt kalır)

**Önerilen zaman:** Faz 5 — KVKK uyum şartı, hukuki süre baskısı
varsa öncelik. Tahmini iş: ~2 gün (servis + UI + test + e-posta
onay flow).

---

## ✅ 20. Bot/spam detection (yorum + şikayet rate limiting) — ÇÖZÜLDÜ (Adım 5.2, 15 Mayıs 2026)

**Çözüm:** ASP.NET Core `RateLimiter` middleware Adım 5.2'de eklendi; 5 named
policy: `comment` (dakikada 5), `report` (saatte 10), `message` (dakikada 30),
`connection` (saatte 20), `ajax-general` (dakikada 60). Per-user partition.
`ChainedRateLimiter` (saat+dakika çift limit) Faz 6+'a bırakıldı (kısmen — charter
Karar 7 spec gereği).

---

## 20. (Orijinal kayıt — referans için)

### Bot/spam detection (yorum + şikayet rate limiting)

**Bağlam:** Adım 4.9 (yorum AJAX) ve 4.10 P1 (şikayet AJAX) anti-forgery
korumalı ama rate limiting yok.

**Mevcut durum:** Bir kullanıcı saniyeler içinde 100 yorum yazabilir
veya 100 farklı içeriği şikayet edebilir. Mükerrer şikayet engelli
(ReporterId+TargetType+TargetId unique) ama farklı targetlere şikayet
sınırsız.

**İdeal çözüm:**
- ASP.NET Core Rate Limiting (built-in `Microsoft.AspNetCore.RateLimiting`):
  - Kullanıcı başına dakikada 5 yorum
  - Kullanıcı başına saatte 10 şikayet
- Honeypot field (bot otomatik dolduran trap input) — spam pattern
  detection
- Şüpheli pattern (örn. 3 şikayet 10 dk içinde → admin notification)

**Önerilen zaman:** Faz 5 başı — public traffic artmadan önce.
Tahmini iş: ~1 gün.

---

## ✅ 21. Comment edit history — ÇÖZÜLDÜ (Adım 6.8, 1 Haziran 2026)

**Çözüm:** `PostCommentRevision` entity (`Id, CommentId, OriginalBody,
OriginalCreatedAt, FirstEditedAt`) + `AddPostCommentRevisions` migration.
`HasOne...WithOne` + unique index → yorum başına en fazla 1 revision.
`PostCommentService.UpdateAsync` İLK düzenlemede orijinali saklar; sonraki
düzenlemeler revision'a DOKUNMAZ (no-op/aynı body edit revision yaratmaz).
`GET /api/post-comments/{id}/original` (anon, gizli/yayında-olmayan yorum
filtreli) + `_PostComment.cshtml` "düzenlendi · (orijinali göster)" toggle
+ `comment-original.js` lazy fetch. Yorum silinince cascade ile silinir
(KVKK). İdeal plandan sapma: tam revision geçmişi (son 5) yerine yalnız
İLK orijinal saklanır (moderasyon "spam→benign düzenleme" senaryosu için
yeterli; charter kararı). `EditedByUserId` yok — düzenleme yalnız sahip
tarafından yapılabildiğinden gereksiz. Test: `CommentEditHistoryTests` (5).

---

## 21. (Orijinal kayıt — referans için)

**Bağlam:** Adım 4.9'da `PostComment.IsEdited` flag eklendi
(UI "düzenlendi" rozet) ama eski içerik kaybediliyor.

**Mevcut durum:** Yorum düzenlenirse eski hâli yok. Moderasyon
veya tartışma için "yorum eskiden ne diyordu?" cevabı verilemez
(spam yorumu sahip düzenleyip benign hâle getirmiş olabilir).

**İdeal çözüm:** `PostCommentRevision` tablosu:
- `Id, CommentId, OldBody, EditedAt, EditedByUserId`
- `PostCommentService.UpdateAsync` her güncelleme öncesi revision yazar
- Admin UI: yorum detayında "geçmiş" linki (son 5 revision)

**Önerilen zaman:** Faz 5 — moderasyon kararı yanlış yöne giderse
öncelik artar. Tahmini iş: ~1 gün.

---

## 22. Notification email kanalı

**Bağlam:** Faz 3.3'te bell icon notification var, Faz 4'te yeni
tipler eklendi (Connection, Comment, Like, ContentRemoved, ...).
Kullanıcı her zaman platformda olmuyor — önemli olaylar email ile
de gitsin.

**Mevcut durum:** Notification sadece bell icon. Kullanıcı bağlantı
isteği aldığında email gelmiyor. Karşılaştırma: LinkedIn email
notifications güçlü.

**İdeal çözüm:**
- `IEmailService` Faz 3'te zaten var (LoggingEmailService +
  production SMTP adapter)
- `NotificationService.CreateAsync` sonrası kullanıcının email
  tercihine göre email tetikle
- Kullanıcı tercih ayarı: `UserNotificationPreferences` entity
  - `EmailOnConnectionRequest` (default true)
  - `EmailOnComment` (default false — gürültü)
  - `EmailOnContentRemoved` (default true)
  - vs.
- Hangfire job (immediate veya 15 dk batch)

**Önerilen zaman:** Faz 5 — kullanıcı geri çağırma ihtiyacı belirginleşince
(retention metric). Tahmini iş: ~2-3 gün.

---

## ✅ 23. Authorize sayfaları için otomatik NoIndex — ÇÖZÜLDÜ (Adım 5.3, 15 Mayıs 2026)

**Çözüm:** Endpoint metadata middleware Adım 5.3'te eklendi: bir endpoint
`AuthorizeAttribute` taşıyorsa response'a `X-Robots-Tag: noindex, nofollow`
header'ı otomatik eklenir. Manuel `<meta name="robots">` koymaya gerek yok.
`NoIndexAutoTests` ile doğrulandı (admin/profil/hesaplarım sayfaları noindex).

---

## 23. (Orijinal kayıt — referans için)

### Authorize sayfaları için otomatik NoIndex

**Bağlam:** Adım 4.6 P2'de `/makalelerim` için NoIndex meta tag eklendi
(robots.txt indeks önler ama view-source temizliği için meta de).
Diğer `[Authorize]` sayfalar (`/baglantilarim`, `/profil`, `/admin/*`)
kontrol edilmedi.

**Mevcut durum:** Bazı authenticated sayfalarda NoIndex meta yok.
Robots.txt zaten engelliyor ama defense-in-depth eksik.

**İdeal çözüm:** `_Layout.cshtml`'de otomatik NoIndex:
```razor
@if (User.Identity?.IsAuthenticated == true && ViewData["IsAuthenticatedPage"] != null)
{
    <meta name="robots" content="noindex, nofollow" />
}
```
Veya middleware/filter pattern: `[Authorize]` attribute olan
sayfalar otomatik `X-Robots-Tag: noindex` header.

**Önerilen zaman:** Faz 5 minor cleanup. Tahmini iş: ~30 dk.

---

## ✅ 24. Mesajlar polling — sayfa görünürlüğü/sekme aktivasyonu duyarsız — ÇÖZÜLDÜ (Adım 6.7, 30 Mayıs 2026)

**Çözüm:** `mesajlar.js` `visibilitychange` listener — `document.hidden` ise
`stopPolling()` (clearInterval), öne gelince `poll()` (kaçırılanı anında çek) +
`startPolling()`. `startPolling`/`stopPolling` idempotent guard'lı. SignalR-aktif
skip mantığı korundu. İlk yükleme `!document.hidden` ise başlar.



**Bağlam:** Adım 5.5'te `/mesajlar/{id}` Detail sayfası 30 sn'de bir
`/api/messages/{id}/new` polling yapıyor. Sayfa arka planda (sekme
inactive) olduğunda da polling devam ediyor.

**Mevcut durum:** `setInterval(poll, 30000)` sürekli aktif. Tarayıcı
sekmesi gizli olsa bile her 30 sn'de bir HTTP request gidiyor.
Multi-tab kullanıcılarda gereksiz trafik.

**İdeal çözüm:** `document.visibilityState === 'visible'` kontrol
yapılsın; `visibilitychange` event'inde polling pause/resume.
Veya tek seferlik short-poll: visible tab içinde aktif, hidden olunca
durur.

**Önerilen zaman:** Adım 5.6 (SignalR) ile birlikte doğal olarak çözülür
(WebSocket connection sekme inactive olduğunda zaten boşa eden
yok). SignalR fallback olarak polling kalacaksa o zaman bu kuralı
uygula.

---

## ✅ 25. Mesajlar 'Daha fazla yükle' aktif değil (≡ eski #30) — ÇÖZÜLDÜ (Adım 6.7, 30 Mayıs 2026)

**Çözüm:** Yeni `GET /api/messages/{convId}/older?skip=&take=` endpoint —
server-rendered `_Message` HTML array + `hasMore` (GetNewSince HTML pattern reuse;
GetByConversation VM sözleşmesi bozulmadı). `mesajlar.js` load-more: `afterbegin`
prepend (DESC → kronolojik) + scroll pozisyon koruması; hasMore=false'ta buton gizlenir.
MessagesApiControllerTests `GetOlder_...HasMore` ile doğrulandı.



**Bağlam:** Adım 5.5'te `/mesajlar/{id}` Detail sayfasında HasMore=true
durumunda "Daha fazla yükle" butonu render ediliyor, ama tıklamada
alert gösteriyor (placeholder).

**Mevcut durum:** GET /api/messages/{conversationId}?skip=N&take=50
endpoint'i var ve VM array dönüyor. Ancak client-side render server
template ile uyumsuz (server _Message partial render eder, client VM
yapısını render edebilecek bir JS template fonksiyonu yazılmadı).

**İdeal çözüm:**
- Server-side render endpoint: GET /api/messages/{convId}/page?skip=N
  → HTML string array döner (server _Message partial). Client
  insertAdjacentHTML('afterbegin') ile prepend eder.
- Veya client template: handlebars/lit-html ile MessageViewModel'den
  HTML üret (ama XSS riski + duplicate code).

**Önerilen zaman:** Adım 5.6 (SignalR) ile birlikte tasarım yenilemesi
sırasında veya Faz 6+ pagination iyileştirmesi. Pratikte 50'den fazla
mesajı olan konuşma az olduğu için düşük öncelik (eski #30 notu).

---

## 26. MessagesController polling endpoint avatar URL kullanıcı bağımsız

**Bağlam:** Adım 5.5'te `/api/messages/{id}/new` polling yanıtı server
tarafında _Message partial HTML üretir. Avatar URL `IMediaStorage.GetPublicUrl`
çağrısı ile resolve edilir; bu çağrı user-agnostic (CDN URL üretir).

**Mevcut durum:** Pratik bir sorun yok — avatar URL'ler statik. Ama
ileride avatar erişim kontrolü (private avatars) eklenirse VM bina
mantığı `viewerId` parametresini değerlendirmeli.

**İdeal çözüm:** Avatar URL'leri her zaman context-aware bir helper
üzerinden resolve etsin (UserAvatarUrlResolver) — gelecekte rule
değişirse tek noktadan dokunulur.

**Önerilen zaman:** Avatar privacy gereksinimi doğarsa (private/blocked
user avatar gizleme) gündeme alınır. Şimdilik MEYP.

---

## 27. SignalR @microsoft/signalr CDN — self-host önerilir

**Bağlam:** Adım 5.6'da `/mesajlar/{id}` Detail sayfası SignalR client
kütüphanesini cdnjs.cloudflare.com'dan yüklüyor (8.0.7).

**Mevcut durum:** External CDN bağımlılığı: CDN downtime → SignalR
yüklenmez → polling fallback devreye girer (sessiz). Integrity hash
yok (yanlış hash sayfayı kırar; CDN'in TLS güvenliği yeterli).

**İdeal çözüm:** `npm install @microsoft/signalr` + statik dosya olarak
`wwwroot/lib/signalr/` altına copy + integrity hash + asp-fallback-src
pattern (Bootstrap pattern reuse). Veya LibMan kullanılabilir.

**Önerilen zaman:** Production öncesi (Faz 6+ release prep). Şimdi CDN
geliştirme/test için pratik.

---

## 28. SignalR tek instance — multi-instance Redis backplane

**Bağlam:** Adım 5.6'da SignalR Hub default in-memory backplane ile
çalışıyor. AddSignalR() varsayılan konfig.

**Mevcut durum:** Tek instance deployment (sticky sessions OK). Multi-
instance'da kullanıcı A instance-1'e bağlı, kullanıcı B instance-2'ye
bağlı → A'dan B'ye broadcast çalışmaz (her instance kendi grup
listesini tutuyor).

**İdeal çözüm:** `Microsoft.AspNetCore.SignalR.StackExchangeRedis`
NuGet paketi + `AddStackExchangeRedis(connectionString)`. Redis zaten
projede mevcut (rate limit / cache).

**Önerilen zaman:** Multi-instance horizontal scale gereksinimi
doğduğunda (Faz 6+). Tek instance'la başlangıç yeterli.

---

## 29. SignalR negotiation rate limit yok

**Bağlam:** Adım 5.6'da `/hubs/messages/negotiate` endpoint Hub
[Authorize] dışında özel rate limit attach edilmedi.

**Mevcut durum:** Anonim 401 dönüyor ama brute-force negotiate kullanım
limit yok. Authenticated kullanıcı için açık (otomatik reconnect zaten
makul).

**İdeal çözüm:** `[EnableRateLimiting("ajax-general")]` Hub negotiate'a
veya yeni "signalr-negotiate" policy (5/dakika).

**Önerilen zaman:** Production öncesi cleanup. Düşük öncelik —
[Authorize] negotiate spam'ı önlüyor zaten.

---

## 30. Mesaj 'Daha fazla yükle' sayfalama placeholder — DUPLICATE → #25

**Not (Adım 6.0 denetimi, 29 Mayıs 2026):** Bu madde #25 ("Mesajlar
'Daha fazla yükle' aktif değil") ile aynı konuyu anlatıyordu — Adım 5.5
raporlamasında yanlışlıkla iki kez kaydedildi. **Kanonik kayıt: #25**
(daha ayrıntılı; iki çözüm seçeneği + XSS notu içeriyor). #30'un tek
özgün notu ("50+ mesajlı konuşma az → düşük öncelik") #25'e taşındı.
Numara dosya stabilitesi için silinmedi.

---

## ✅ 31. ConversationService GetForUserAsync n+1 query — ÇÖZÜLDÜ (Adım 6.10, 1 Haziran 2026)

**Çözüm:** `GetForUserAsync` tek SELECT'e indirildi. Önceden N conv için
**(2N+2)** round-trip (1 conv listesi + 1 engelleme listesi + N son-mesaj + N
unread); şimdi **1 query**. Engelleme filtresi korelasyonlu `EXISTS`, son mesaj
preview + unread korelasyonlu alt sorgu (SQL Server `APPLY`), görüntüleme alanları
(isim/slug/avatar) skalar projekte edilip in-memory `ApplicationUserDisplayExtensions`
ile map'lenir (anonimize/inactive fallback tek-kaynak korunur). Kanıt:
`ConversationServicePerformanceTests` (`QueryCounterInterceptor` ile gerçek
LocalDB'de `Count==1`). Davranış regresyonu 0 (12 mevcut ConversationServiceTests
yeşil). Yeni reuse edilebilir test pattern: `TestHelpers/QueryCounterInterceptor`.

---

## 31. (Orijinal kayıt — referans için)

**Bağlam:** Adım 5.4'te `ConversationService.GetForUserAsync` her
conversation için ayrı `LastMessage` ve `UnreadCount` query'si yapıyor.

**Mevcut durum:** Kullanıcı başına 100+ konuşma olduğunda 100+ query.
Az kullanıcı için sorun yok.

**İdeal çözüm:** Tek query ile JOIN/aggregate (window function veya
GroupBy), .NET 10 EF GroupBy projection iyileştirilmiş.

**Önerilen zaman:** Aktif kullanıcı sayısı / mesajlaşma yoğunluğu
arttığında profile et + optimize.

---

## ✅ 32. GetUnreadCountAsync n+1 query — ÇÖZÜLDÜ (Adım 6.10, 1 Haziran 2026)

**Çözüm:** Tek `SELECT COUNT`'a indirildi. Önceden **(N+1)** round-trip (1 conv
listesi + N `Messages.CountAsync`); şimdi **1 query** — `Message.Conversation`
navigation üzerinden filtre, viewer'ın User1/User2 olmasına göre ilgili
`LastReadAt` eşiği. Üst menü `UnreadMessagesBadge` ViewComponent her authenticated
request'te bu metodu çağırdığından kazanç layout render'a doğrudan yansır. Kanıt:
`GetUnreadCountAsync_SumsAcrossAllConversations_SingleQuery` (`Count==1`). Davranış
korundu (`GetUnreadCountAsync_CountsOnlyOtherSenderAfterLastRead` yeşil).

---

## 32. (Orijinal kayıt — referans için)

**Bağlam:** Adım 5.4 + 5.7'de `GetUnreadCountAsync` her conversation
için ayrı `Messages.CountAsync` çağrısı yapıyor (badge için).

**Mevcut durum:** Üst menü her authenticated request'te bu metodu
çağırıyor (UnreadMessagesBadge ViewComponent). Konuşma sayısına
oranlı n query.

**İdeal çözüm:** Tek aggregate query (Messages JOIN Conversations,
GroupBy ConversationId, sum). Veya cache: kullanıcı başına sayım Redis
(SignalR push ile invalidate).

**Önerilen zaman:** Layout render performance metrik düştüğünde.

---

## 33. Admin mesaj Detail konuşma context yok

**Bağlam:** Adım 5.7'de admin moderasyon paneli `/admin/sikayetler/3/{id}`
yalnızca şikayet edilen mesajı render ediyor — conversation'ın diğer
mesajları görünmüyor (KVKK + admin sınırı).

**Mevcut durum:** Admin tek mesaj üzerinden karar veriyor. Gerçek
context (önce/sonra ne yazıldı?) için kullanıcıdan ek bilgi gerekirse
manuel takip lazım.

**İdeal çözüm:** Admin'e "tüm konuşmayı incele" özel butonu (audit
log'a yazılır), context view sadece şikayet inceleme süresince görünür.

**Önerilen zaman:** Faz 6+ moderasyon iyileştirme. Şimdilik karar:
admin tek mesaj görür, conversation context yok (KVKK gözetildi).

---

## ✅ 34. Test altyapısı hibrit — InMemory + SQL Server LocalDB bir arada — Kapatıldı (2026-05-15, Adım 5.8 P2)

**Çözüm:** Hibrit son. Kalan ~90 sınıf SQL Server LocalDB fixture'ına geçirildi
(4 paralel düzeltme dalgası). `MakeUser(int id)` helper'ları `MakeUser(string suffix)`
şeklinde dönüştürüldü, explicit `Id = N` atamaları kaldırıldı; literal Id
referansları DB-generated `.Id` değişkenleriyle değiştirildi. Tenant+User circular FK
seed'leri üç aşamalı `SaveChanges`'e bölündü (user TenantId=null → Tenant
OwnerUserId → user.TenantId update). `UserProfiles.BaroNo` unique-index çakışması:
test seed'leri `BaroNo = null` (assertion yoktu). `ContentReportsAdminController`
admin seed'i: `ReviewedByUserId` FK için gerçek admin kullanıcı yaratılıp `X-Test-UserId`
header'ı buna bağlandı. LocalDB connect timeout 60 sn'ye çıkarıldı (parallel test
burst altında transient timeout'ları önler). `TestDbContextFactory.cs` ve InMemory
`WebApplicationFactoryFixture.cs` silindi; `AuthTestHelper.cs`'den InMemory varyant
çıkarıldı (`TestAuthHandler` korundu — SqlServer varyant kullanıyor);
`Microsoft.EntityFrameworkCore.InMemory` NuGet referansı kaldırıldı.
Final: 779/779 yeşil. Bkz. [[#11]].

---

## 34. (Orijinal kayıt — referans için)

### Test altyapısı hibrit — InMemory + SQL Server LocalDB bir arada

**Bağlam:** Adım 5.8 P1 (charter Karar 10). InMemory EF Core provider
gerçek SQL Server semantiğini taklit etmiyor (IDENTITY kolonları, FK
insert sıralaması, collation, ExecuteUpdate/GroupBy translation). Test
güveni için SQL Server LocalDB tabanlı fixture'a geçiş başlatıldı; P1
yalnızca altyapı + 4 pilot sınıf + kırılma tespiti yaptı.

**Mevcut durum:** İki fixture paralel duruyor. Pilot 4 sınıf
(`ConversationServiceTests`, `MessagesApiControllerTests`,
`MesajlarPageTests`, `HideModerationTests`) `SqlServerTestFixture` /
`SqlServerTestAuthWebApplicationFactory` kullanıyor; kalan ~90 sınıf
hâlâ InMemory `TestDbContextFactory` / `TestAuthWebApplicationFactory`
kullanıyor. Pilot servis sınıflarında 21 test kırık (20× explicit
identity Id seed → `IDENTITY_INSERT OFF`; 1× Tenant↔User circular FK).
Integration testler (18) SQL Server'da sorunsuz geçti.

**İdeal çözüm:** Tüm test sınıfları SQL Server LocalDB fixture'ına
geçirilir, InMemory provider ve `TestDbContextFactory` tamamen kaldırılır.
Seed helper'larındaki explicit `Id = 1,2,3` kullanımı bırakılır (DB
üretir, üretilen Id yakalanır); circular FK seed'leri iki aşamalı
SaveChanges'e bölünür; `IClassFixture` paylaşımlı DB nedeniyle testler
arası izolasyon stratejisi netleştirilir (per-test transaction rollback
veya per-test DB).

**Önerilen zaman:** Adım 5.8 P2 — kalan sınıfların geçişi + 21 kırık
testin düzeltilmesi. 21 kırılma >20 eşiğini geçtiği için P2 stratejisi
(toplu geçiş mi, kademeli mi; izolasyon modeli) başlamadan önce gözden
geçirilmeli.

---

# Adım 6.0 Denetiminde Eklenen Belgelenmemiş Maddeler (29 Mayıs 2026)

Faz 4-5 boyunca karar/uyarı olarak ortaya çıkmış ama tech-debt'e
işlenmemiş 6 madde. Adım 6.0 (Faz 5 sonu denetim) sırasında tespit edilip
deftere alındı. Detay: `docs/phase-6-scope-inventory.md` §3.

---

## ✅ 35. NU1901 — NuGet paket güvenlik açığı (transitive) — ÇÖZÜLDÜ (Adım 6.4, 30 Mayıs 2026)

**Çözüm:** GHSA-g4vj-cjjj-v7hg → 6.12.x hattında ilk yamalı sürüm **6.12.5**
(vulnerable 6.12.0-6.12.4). `LexCalculus.Web.csproj`'a transitive pinning
(Strateji 1): `NuGet.Packaging` + `NuGet.Protocol` 6.12.5 explicit PackageReference.
Sahip (`Microsoft.VisualStudio.Web.CodeGeneration.Design`) 6.12.1 çekiyordu;
pinning override etti. Build 0 NU1901, version conflict yok, 798/798 yeşil.

**Bağlam:** Faz 4-5 boyunca her `dotnet build` çıktısında NU1901 uyarısı
göründü; hiçbir adımda kapatılmadı veya deftere alınmadı.

**Mevcut durum:** `LexCalculus.Web` build'inde `NuGet.Packaging` 6.12.1 ve
`NuGet.Protocol` 6.12.1 paketlerinde düşük önem dereceli bilinen güvenlik
açığı (GHSA-g4vj-cjjj-v7hg) uyarısı veriliyor. Transitive bağımlılık
(codegenerator/scaffolding tooling zinciri), runtime path'e girmiyor →
çalışan üründe istismar yüzeyi yok. Ama build gürültüsü + "açık var"
sinyali.

**İdeal çözüm:** Geçişli paketleri yamalı sürüme zorla — ya `dotnet list
package --vulnerable --include-transitive` ile kaynağı bul + üst paketi
güncelle, ya da `Directory.Packages.props` / `<PackageReference>` ile
`NuGet.Packaging` ve `NuGet.Protocol` ≥ yamalı sürüme pin'le. Geçişli
sahibi tooling ise tooling sürümünü yükselt.

**Önerilen zaman:** Faz 6 (güvenlik önceliği). Düşük önem ama "açık var"
durumu kapatılmalı. Tahmini iş: ~0.5 saat.

---

## ✅ 36. CA2024 — async metotta `reader.EndOfStream` kullanımı — ÇÖZÜLDÜ (Adım 6.4, 30 Mayıs 2026)

**Çözüm:** `LifeTableCsvParser.ParseAsync` veri-satırı döngüsü
`while (!reader.EndOfStream) { line = await ReadLineAsync() }` →
`while ((rawLine = await reader.ReadLineAsync(ct)) != null)`. EOF'ta null,
boş satırda "" döndüğü için `lineNumber`/blank-skip semantiği birebir korundu.
CSV + LifeTable testleri (20) yeşil — fonksiyonel eşdeğer.

**Bağlam:** Build analyzer uyarısı (CA2024). Faz 1-2'de yazılan CSV parser
kodunda, .NET 10 analyzer'ı tarafından işaretleniyor.

**Mevcut durum:** `LexCalculus.Infrastructure/Services/Csv/LifeTableCsvParser.cs:43`
async bir metotta senkron `StreamReader.EndOfStream` property'sini kullanıyor.
CA2024: async akışta `EndOfStream` gizli senkron blocking yapabilir; doğrusu
`ReadLineAsync` döngüsü + null kontrolü. Fonksiyonel hata değil (LifeTable CSV
küçük), sadece kod kalitesi.

**İdeal çözüm:** `while ((line = await reader.ReadLineAsync(ct)) != null)`
döngüsüne çevir, `EndOfStream` kontrolünü kaldır.

**Önerilen zaman:** Faz 6 (küçük temizlik). Tahmini iş: ~15 dk + test
regresyon kontrolü.

---

## 🟡 37. SignalR multi-tab mark-as-read race condition — KISMEN ÇÖZÜLDÜ (Adım 6.7, 30 Mayıs 2026)

**Kısmi çözüm:** Backend foundation hazır. `IMessagingNotifier.NotifyConversationReadAsync`
(4. method) eklendi; `ConversationService.MarkAsReadAsync` sonunda kullanıcının
`user-{id}` grubuna `ConversationRead` broadcast (best-effort, sessiz fail).
`mesajlar.js` Detail handler şu an **no-op** (Detail zaten okundu state'inde).
**KALAN (Faz 7+):** Liste sayfası (`/mesajlar`) SignalR'a bağlanmıyor; bir tab
okuyunca diğer tab'ın liste unread badge'i real-time güncellenmiyor (sayfa
yenilemede güncel). Tam çözüm liste sayfasına SignalR + badge update gerektirir.



**Bağlam:** Adım 5.6 (SignalR) sırasında konuşuldu ama tech-debt'e eklenmedi.
Charter §12 Faz 6 önizlemesinde "#26" numarasıyla anılmıştı — ama #26 aslında
"avatar URL user-agnostic" maddesi; bu race condition'ın kaydı hiç açılmamıştı.

**Mevcut durum:** Aynı kullanıcı iki sekmede aynı konuşmayı açık tutarsa:
Tab A mesajı görüntüleyip mark-as-read tetikler, Tab B'deki okunmamış sayacı
(badge) ve "yeni" işaretleri eski kalır. SignalR mark-read event'i diğer
sekmeye broadcast edilmediği için iki sekme arası senkron bozulur. Pratik
etki düşük (kozmetik badge tutarsızlığı), veri kaybı yok.

**İdeal çözüm:** Mark-read olayını kullanıcının `user-{id}` SignalR grubuna
broadcast et (kendi diğer sekmeleri dahil) → tüm açık sekmeler badge'i
günceller. `ConversationRead` event + client handler.

**Önerilen zaman:** Faz 6 D kümesi (UX iyileştirme). Tahmini iş: ~0.5 gün.

---

## 38. IPartialRenderer reuse pattern refactor (mesaj VM + render duplikasyonu)

**Bağlam:** Adım 5.7'de bahsedildi; charter §12'de "#28" numarasıyla anıldı —
ama #28 aslında "SignalR Redis backplane"; bu refactor maddesinin kaydı
açılmamıştı. (`IPartialRenderer` primitive'i zaten mevcut ve paylaşılıyor;
borç, onun ÜZERİNDEKİ kompozisyonun tekrar etmesi.)

**Mevcut durum:** "Bir `Message` entity'sinden, belirli bir viewer için
`MessageViewModel` kur (avatar URL `IMediaStorage.GetPublicUrl`, displayName
`GetDisplayNameOrAnonymized`, IsOwnMessage perspektifi) + `_Message` partial'ı
render et" dizisi iki yerde neredeyse birebir tekrarlanıyor:
- `MessagesController` (Send / GetNewSince polling endpoint'leri)
- `SignalRMessagingNotifier.NotifyMessageReceivedAsync`

DRY ihlali; mesaj VM şekli değişirse iki yer senkron güncellenmeli.

**İdeal çözüm:** `IMessageHtmlRenderer.RenderForViewerAsync(Message msg,
int viewerId, CancellationToken ct)` helper extract et — VM kurma +
`_partial.RenderAsync("_Message", vm)` tek noktada. İki çağıran da bunu
kullanır.

**Önerilen zaman:** Faz 6 F kümesi (performance/cleanliness). Tahmini iş:
~0.5 gün.

---

## 39. NotificationsEmailEnabled orphan field (kullanılmayan alan)

**Bağlam:** `ApplicationUser.NotificationsEmailEnabled` alanı Faz 3'te eklendi
(`AddNotificationsEmailEnabledToUser`, migration 20260428132236). Eklendiği
andan beri hiçbir serviste OKUNMUYOR — dead field. (Not: bu alan ayrıca
tech-debt #3'teki "EF migration default" tuzağının da kaynağıydı.)

**Mevcut durum:** Kolon DB'de var, kullanıcı başına bir bool tutuyor ama hiçbir
akış değerini sorgulamıyor. Email notification kanalı (madde #22) hiç
uygulanmadığı için alan boşta duruyor.

**İdeal çözüm:** Faz 6 B kümesi (email notification, #22) bu mevcut flag'i
tüketsin — `NotificationService.CreateAsync` sonrası email tetiklemeden önce
`user.NotificationsEmailEnabled` kontrol edilsin. Per-tür opt-in gerekirse
`UserNotificationPreferences` entity'sine genişletilir; bu coarse flag "global
email aç/kapa" olarak kalır veya migrate edilir. Yani silinmez — #22'nin
başlangıç noktası.

**Önerilen zaman:** Faz 6 B kümesi (email kanalı uygulanırken). İş #22 ile
birlikte.

---

## 🟡 40. Polling fallback manuel test borcu (Adım 5.6 Senaryo 5) — ADIM 6.13'E TAŞINDI

**Durum (Adım 6.9, 1 Haziran 2026):** Adım 6.7'de tarayıcı smoke'u DENENDİ ancak
**yanlış senaryo** seçildi: DevTools "Network Offline" tüm HTTP trafiğini keser →
SignalR hem de polling fallback aynı anda ölür, dolayısıyla "WS kopunca polling'e
düşüyor mu" sorusu test EDİLEMEZ. Doğru senaryo: **WebSocket bloke + HTTP açık**
(ör. DevTools'tan yalnız WS handshake'i blokla, veya SignalR transport'u zorla
long-polling dışına it). Bu nedenle borç kapatılmadı; **Adım 6.13 Faz 6 closeout**
bütünsel manuel smoke'una taşındı (doğru senaryo orada uygulanacak). Kod yolu hazır,
`GetNewSince` integration testi ile otomatik kapsanıyor — bloklayıcı değil.

**Durum (Adım 6.7, 30 Mayıs 2026):** Polling kod yolu Adım 6.7'de elden geçirildi
(#24 görünürlük) ve `GetNewSince` integration testi ile otomatik kapsanıyor. ANCAK
"SignalR kopuk → tarayıcıda gerçekten polling'e düşüyor mu" uçtan uca smoke'u
(DevTools Network offline/WS block) **otomatik ajan tarafından yapılamadı** —
gerçek tarayıcı + network throttling gerektiren bir insan adımı. Kod hazır;
**tarayıcı smoke'u kullanıcıya kaldı** (~2 dk: DevTools → WS bağlantısını blokla →
karşı taraf mesaj at → 30 sn içinde polling request + mesaj gelişini gözle).
Faz 6.13 closeout öncesi yapılması önerilir.

**Bağlam:** Adım 5.6 (SignalR) manuel doğrulama Senaryo 5 — "SignalR bağlantısı
kopuk → 30 sn polling fallback devreye girer" — kullanıcı tarafından
"deneyemedim" notuyla **doğrulanmadı**. Faz 5 kapanışında (`phase-5-complete`)
açık kaldı.

**Mevcut durum:** Polling fallback kod yolu mevcut ve integration test'lerle
dolaylı kapsanıyor (MessagesApiControllerTests GetNewSince), ama "WebSocket
kopunca tarayıcıda gerçekten polling'e düşüyor mu" uçtan uca tarayıcı
doğrulaması yapılmadı. Adım 6.0 kararı: bloklayıcı değil — otomatik test'lerle
dolaylı kapsanmış kabul edilir, manuel doğrulama Faz 6'ya taşınır.

**İdeal çözüm:** Bütünsel mesajlaşma smoke testi içinde: DevTools'tan WS
bağlantısı bloklanır, 30 sn polling akışı + yeni mesaj gelişi gözlemlenir.

**Önerilen zaman (revize, Adım 6.5):** Dalga A'da (email) yapılmadı —
mesajlaşma çekirdeğine dokunulmadığı için ertelendi. **Adım 6.7** (polling
görünürlük #24 + multi-tab race #37 + sayfalama #25 refactor'u) polling kod
yolunu zaten elden geçirecek; smoke o adımda yeni davranışla birlikte yapılır.
Tahmini iş: ~10 dk smoke.

---

## 41. Envanter denetimleri usage/grep taraması içermeli (süreç)

**Bağlam:** Adım 6.0 envanteri `NotificationsEmailEnabled`'ı (#39) "Faz 3'ten
beri unwired orphan" diye etiketledi. Adım 6.2 P1/P2'de bunun **yanlış** olduğu
ortaya çıktı: alan hem `DataFreshnessCheckJob` (sistem/tazelik e-postaları opt-out)
hem de `/profil` toggle'ı tarafından **aktif kullanılıyordu**. Bu yanlış etiket
charter §3 Karar 3'e "deprecate + DropColumn" olarak sızdı; uygulanmadan önce
yakalanıp master-switch-korunur tasarıma çevrildi (drop edilseydi tazelik
e-postası opt-out'u ve profil toggle'ı kırılırdı).

**Mevcut durum:** Envanter denetimi (Adım 6.0) bir alanın "kullanılıyor mu"
sorusunu **dosya/property varlığına** göre yanıtladı; gerçek **referans taraması**
(grep `NotificationsEmailEnabled`) yapmadı. "Orphan" iddiaları bu yüzden güvenilmez.

**İdeal çözüm:** Envanter/denetim adımlarında bir alanı "kullanılmıyor" ilan
etmeden önce zorunlu usage taraması: `grep -r <symbol>` (servis + job + view +
test) + en az 1 referansın anlamı doğrulanır. "Dosya var ama çağıran yok"
kanıtlanmadan orphan denmez.

**Önerilen zaman:** Süreç kuralı — Faz 6 closeout (Adım 6.13) retrospektifinde
CLAUDE.md veya denetim şablonuna eklenir. Kod borcu değil, denetim-kalitesi borcu.

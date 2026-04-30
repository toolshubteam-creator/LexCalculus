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

# Lex Calculus — Claude Code Bağlam Notları

Bu dosya yeni bir konuşma açıldığında Claude Code'un proje üzerinde
çalışırken bilmesi gereken kalıcı kuralları içerir.

## Tech debt defteri

Yeni bir mimari karar verdiğimizde, bilinçli bir sapma yaptığımızda,
veya "şimdi yeterli ama bir gün dokunulmalı" niteliğinde bir kararla
karşılaştığımızda → mutlaka `docs/tech-debt.md` dosyasına 4-başlıklı
format ile bir madde ekle:

1. **Bağlam:** Hangi adımda, hangi koşulda ortaya çıktı
2. **Mevcut durum:** Ne yapıldı (geçici çözüm)
3. **İdeal çözüm:** Doğrusu nasıl olmalıydı
4. **Önerilen zaman:** Ne zaman dokunulması mantıklı

Faz sonlarında bu defter gözden geçirilir, çözülenler "ÇÖZÜLDÜ" notuyla
arşive taşınır.

## Operasyon notları

`docs/operations.md` runbook ve operasyonel detayları içerir
(cache invalidation, Hangfire dashboard, e-posta provider). Yeni
operasyonel bilgi (örn. yeni cron job, yeni external service) bu
dosyaya yazılır.

## Geliştirme standartları (kısa hatırlatma)

Detay için README.md'ye bakın. Özet:

- **Inline `style="..."` yasak.** Tüm CSS `wwwroot/css/` altında BEM ile.
- **EF Core migration:** Yeni nullable olmayan default değerli alan
  eklerken üretilen migration dosyasının `defaultValue` kısmını
  manuel kontrol et (geçmiş bir tuzak — bkz. tech-debt.md madde 3).
- **E-posta şablonları istisna:** `Views/Emails/` altındaki .cshtml
  dosyalarında inline style ZORUNLU (e-mail clients external CSS
  strip eder).
- **Test:** Her yeni servis için en az 1 happy path + 1 edge case test.

## Test yazımı: Türkçe karakter encoding

Razor view'lar default `HtmlEncoder` ile çalışır — Türkçe karakterler
(ı, ş, ğ, ü, ö, ç, İ) response body'de HTML entity'lere encode edilir
(örn. `ı` → `&#x131;`). Browser bunu doğru render eder; ama integration
testlerde raw HTML'de string arıyorsan dikkat et:

- **Tercih:** ASCII-only substring kullan
  - YANLIŞ: `Assert.Contains("Hesaplarım", html)`
  - DOĞRU: `Assert.Contains("Hesaplar", html)` veya `Assert.Contains("filtre aktif", html)`
- **Alternatif:** Beklenen string'i runtime'da encode et
  - `var expected = HtmlEncoder.Default.Encode("Hesaplarım");`
- Bu kural Adım 3.4 Parça 1/3'te keşfedildi (commit cc97058).

## Yeni adıma başlamadan önce

Faz/Adım planlamasında yol haritası dosyası tek doğrulama noktasıdır
(`/mnt/user-data/uploads/lex-calculus-faz-3-yol-haritasi.md` veya
projede başka bir konumdaysa o). Bir Adım'ı planlamaya başlamadan ÖNCE:

1. Yol haritasında o Adım'ın bölümünü `view` aracıyla **fiilen oku**.
   Hatırladığın varsayımla devam etme.
2. Bir önceki Adım'ın "ileride neye geçeceğiz" tahminlerini doğrula —
   sıra değişmiş veya yanlış hatırlamış olabilirsin.
3. Sapma fark edersen kullanıcıya açıkça söyle: "Yol haritasında 3.X
   farklıymış, planı düzeltiyorum."

Bu kural, Adım 3.4 sonrası 3.5 planlama sırasında keşfedildi:
"3.5 SEO olacak" demiştim, yol haritası 3.5'i LifeTable olarak yazıyordu.

## Manuel HTTP testlerinde launch profile kullan

`dotnet run --no-launch-profile --urls=http://localhost:XXXX`
KULLANMA. Bu launchSettings.json'ı atlar ve farklı port + farklı
environment kullanır. Tarayıcıdaki gerçek senaryo ile simetrik
DEĞİLDİR.

Doğru komut: `cd LexCalculus.Web && dotnet run` (sade).
launchSettings.json default profilini kullanır — Visual Studio
F5'in kullandığı port + environment ile aynı.

Eğer farklı port gerekiyorsa launchSettings.json'a yeni bir profil
EKLE, run-time `--urls` flag ile override etme.

Bu kural, Adım 3.6 Parça 3/4 sonrası /profil tanı maratonunda
keşfedildi: Claude Code 5099 portunda --no-launch-profile ile run
ediyordu, kullanıcı tarayıcıda 7080'de gerçek server kullanıyordu —
404 görünmesinin kök sebebi port + bind farklılığıydı.

## Razor Pages routing değişikliği = manuel HTTP teyit

ProfilePageTests gibi `WebApplicationFactory<Program>` tabanlı
integration testler **in-memory TestServer** kullanır. Bu, gerçek
Kestrel + port binding + middleware pipeline ile %100 simetrik
DEĞİLDİR.

Yeni Razor Page eklendiğinde veya `@page` direktifi değiştirildiğinde:

1. Integration test yazılır (gerekli)
2. `dotnet run` (launch profile ile, yukarıdaki kural) + tarayıcı veya
   curl ile manuel HTTP doğrulama yapılır (gerekli)

İkisi birden olmadan "sayfa çalışıyor" denmez. Adım 3.6 Parça 3/4
deneyimi: integration test 4/4 yeşil ama Claude Code curl 404
dönüyor görünmüştü. Sebep launch profile + port karışıklığıydı —
ama bu durum integration test'in tek başına yetersiz olduğunu da
gösterdi.

## Windows'ta dotnet process kapatma

`pkill -f "dotnet"` Windows'ta ÇALIŞMAZ (Linux/macOS komutu, sessizce
başarısız olur).

Doğru Windows komutu (Git Bash / cmd):

- Tek process: `taskkill //PID <pid> //F`  (Git Bash double-slash)
- Tüm dotnet: `taskkill //F //IM dotnet.exe`
- PowerShell: `Stop-Process -Name "dotnet" -Force`

Bu unutulduğunda zombie process'ler binary'yi (LexCalculus.Web.exe)
kilitler, build hatası verir veya eski binary'ye curl gider, false
test sonuçları çıkar. Adım 3.6 Parça 3/4 tanı maratonunda bu da
karıştırıcı bir faktördü.

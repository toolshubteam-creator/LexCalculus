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

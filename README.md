# Lex Calculus

Türkiye'deki avukat, hâkim, bilirkişi ve hukuk profesyonellerine yönelik hukuki hesaplama, mevzuat takibi ve mesleki sosyal platform. ASP.NET Core MVC .NET 10 üzerinde geliştirilmiştir.

## Teknoloji Yığını

- **Backend:** ASP.NET Core MVC .NET 10
- **ORM:** Entity Framework Core 10 (Code First)
- **Veritabanı:** Microsoft SQL Server 2022 + Redis (cache)
- **Kimlik Doğrulama:** ASP.NET Core Identity + JWT + OAuth2
- **Real-time:** SignalR
- **Background Jobs:** Hangfire
- **Loglama:** Serilog (Console + File + MS SQL Server)
- **Test:** xUnit + FluentAssertions + Moq

## Klasör Yapısı

- `LexCalculus.Web/` — ASP.NET Core MVC projesi (Controllers, Views, wwwroot)
- `LexCalculus.Core/` — Domain entities, interfaces, business logic (dış bağımlılığı yok)
- `LexCalculus.Infrastructure/` — EF Core, repositories, dış servis entegrasyonları
- `LexCalculus.Jobs/` — Hangfire background job'ları
- `LexCalculus.Tests/` — Unit ve integration testler
- `docs/templates/` — Görsel kimlik referans şablonları

## Tasarım Referansı

`docs/templates/bilirkisi-hesap.html` dosyası projenin **görsel kimlik kaynağıdır**. Tüm CSS Custom Properties (renk paleti, tipografi ölçeği, spacing scale), component dili (tool-card, calc-header, form-section, result-section), tipografik dekoratif elementler (❦ ornament, double gold border, italic burgundy em vurgu) bu dosyadan çıkarılır. Tasarım kararları tahminle değil, kaynaktan elde edilir.

## Geliştirme Standartları

- **Inline `style="..."` yasaktır.** Tüm stiller `wwwroot/css/` altında modüler dosyalara yazılır.
- **CSS Custom Properties zorunludur.** Renkler, fontlar, spacing değerleri sabit yazılmaz; `variables.css` üzerinden referans verilir.
- **BEM metodolojisi** ile class isimlendirmesi: `.block__element--modifier`.
- **Mobil-önce yaklaşım:** Default mobil, breakpoint 900px üzerinde desktop override.
- **EF Core raw SQL yasaktır.** Tüm sorgular parametreli (LINQ veya FromSqlInterpolated).

## Kurulum

_Bu bölüm sonraki fazlarda doldurulacaktır._

## Geliştirme Yol Haritası

- **Faz 1:** Temel altyapı — solution, EF Core, Identity, design system, SEO altyapısı
- **Faz 2:** Hesaplama modülleri — formül parametreleri, A/B/C kategori araçları
- **Faz 3:** Yönetim paneli — admin dashboard, içerik editörü, medya galerisi
- **Faz 4:** Sosyal platform — profil, bağlantı, mesajlaşma (SignalR)
- **Faz 5:** Entegrasyonlar — Hangfire jobs, dış kaynak duyuru çekme, kalan araçlar

## Lisans

Proprietary. © 2026 Lex Calculus.

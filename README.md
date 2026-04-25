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

`docs/templates/bilirkisi-hesap.html` dosyası projenin **görsel kimlik kaynağıdır**. Tüm CSS Custom Properties (renk paleti, tipografi ölçeği, spacing scale), component dili (`tool-card`, `calc-header`, `form-section`, `result-panel`), ve dekoratif elementler (`❦` ornament, double gold border, italic burgundy `<em>` vurgu) bu dosyadan birebir çıkarılmıştır.

Yeni component eklerken bu dosyayı önce inceleyin. Tahmin ile değer atamak yerine kaynaktan örnek alın.

## Geliştirme Standartları

- **Inline `style="..."` kullanımı yasaktır.** Tüm stiller `wwwroot/css/02-components/` altındaki modüler dosyalarda yer alır. Tek seferlik özel değerler için bile component class'ı oluşturulur.
- **CSS Custom Properties zorunludur.** Hex/px/font-family değerleri sabit yazılmaz; `00-tokens/variables.css` üzerinden referans verilir.
- **BEM metodolojisi** ile class isimlendirmesi: `.block__element--modifier`.
- **Mobil-önce yaklaşım:** Default mobil layout, breakpoint 900px üzerinde desktop override.
- **EF Core'da raw SQL yasaktır.** Tüm sorgular LINQ veya `FromSqlInterpolated` (parametreli).
- **Audit timestamp'leri elle set edilmez.** `BaseEntity` türevlerinde `AuditInterceptor` halleder.
- **Repository SaveChanges çağırmaz.** Caller `IUnitOfWork.SaveChangesAsync` ile commit eder.

## Geliştirme Ortamı Kurulumu

### Gereksinimler

- .NET 10 SDK (10.0.202+)
- SQL Server 2022 LocalDB (Visual Studio ile birlikte gelir)
- Visual Studio 2026 veya VS Code + C# Dev Kit
- Git
- (Opsiyonel) Docker Desktop — Redis için
- (Opsiyonel) Node.js LTS — frontend tooling için (Faz 2+)

### Kurulum

```bash
# 1. Klonla
git clone <repo-url> LexCalculus
cd LexCalculus

# 2. NuGet paketlerini yükle
dotnet restore

# 3. User Secrets ile admin password belirle
cd LexCalculus.Web
dotnet user-secrets set "AdminUser:Password" "Admin123!"
cd ..

# 4. Veritabanını oluştur
dotnet ef database update --project LexCalculus.Infrastructure --startup-project LexCalculus.Web

# 5. (Opsiyonel) Redis çalıştır
docker run -d --name lexcalc-redis -p 6379:6379 redis:7-alpine

# 6. Çalıştır
dotnet run --project LexCalculus.Web --launch-profile https
```

Uygulama https://localhost:7080 adresinde çalışır. Default admin kullanıcı: `admin@lexcalculus.local` / User Secrets'taki password.

### Test'leri çalıştırma

```bash
dotnet test
```

25 unit + integration test. Hepsi geçmeli.

### Yeni migration ekleme

```bash
dotnet ef migrations add <MigrationAdı> --project LexCalculus.Infrastructure --startup-project LexCalculus.Web --output-dir Data/Migrations
```

## Health Check Endpoint'leri

| Endpoint | Amaç | Latency |
|---|---|---|
| `/health/live` | Process ayakta mı? (K8s liveness probe) | ~5 ms |
| `/health/ready` | Trafik kabul edebilir mi? DB + cache check (K8s readiness probe) | ~50-2000 ms |
| `/health` | Tüm check'ler — JSON detaylı | aynı |

Cache failure `Degraded` döner (200 OK), sadece DB failure `Unhealthy` (503).

## SEO

- Meta tag, Open Graph, Twitter Card, JSON-LD `SeoMetaViewComponent` ile her sayfada
- `/sitemap.xml` — sitemaps.org 0.9 schema, 1 saat cache
- `/robots.txt` — `/Identity`, `/Admin`, `/api` Disallow

Per-sayfa SEO için controller içinde:

```csharp
ViewData["PageMeta"] = new SeoMeta { Title = "...", Description = "...", JsonLd = "..." };
```

## Yol Haritası

| Faz | Konu | Durum |
|---|---|---|
| 1 | Temel altyapı | Tamamlandı |
| 2 | Hesaplama modülleri (A/B/C kategorileri) | Sonraki |
| 3 | Yönetim paneli (admin, içerik, medya) | Planlı |
| 4 | Sosyal platform (profil, mesajlaşma, SignalR) | Planlı |
| 5 | Entegrasyonlar (Hangfire, dış kaynaklar, kalan araçlar) | Planlı |

## Lisans

Proprietary. © 2026 Lex Calculus.

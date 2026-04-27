# Lex Calculus

**Türkiye'deki avukatlar, hâkimler, bilirkişiler ve hukuk profesyonelleri için
hukuki hesaplama ve içerik platformu.**

ASP.NET Core MVC (.NET 10) tabanlı SaaS platform. Hukuki hesaplama araçları,
güncel mevzuat takibi, mesleki sosyal ağ ve içerik yönetimini tek çatı altında
sunmayı hedefler.

---

## Mevcut Durum (Nisan 2026)

- ✅ **Faz 1 — Temel Altyapı:** Tamamlandı (`phase-1-complete` tag)
- ✅ **Faz 2 — Hesaplama Modülleri:** Tamamlandı (`phase-2-complete` tag)
- ⏳ **Faz 3 — Yönetim Paneli:** Başlamadı
- 📋 **Faz 4 — Sosyal Platform:** Planlı
- 📋 **Faz 5 — Otomatik Veri & Kalan Calculator'lar:** Planlı

### Faz 2 Sonu Sayılar

- **17 aktif hesaplayıcı** (İş Hukuku 7 + Aktüerya 5 + Faiz 5)
- **219 unit test** (TDD ile geliştirme)
- **13 DB tablosu** (CalculationHistory dahil)
- **~95 FormulaParameter satırı** (TÜFE 64, TCMB avans 18, kıdem tavanı 5,
  yasal faiz 2, asgari ücret 3, diğer 3)
- **TRH 2010 LifeTable** (200 satır, erkek + kadın 0-99 yaş)

---

## Aktif Hesaplayıcılar

### İş Hukuku (7)
1. Kıdem Tazminatı (4857 s.K. m.14)
2. İhbar Tazminatı (4857 s.K. m.17)
3. Yıllık İzin Ücreti (4857 s.K. m.53)
4. Fazla Mesai (4857 s.K. m.41)
5. İşe İade Tazminatı (4857 s.K. m.21)
6. Asgari Ücret Uyumluluk
7. Mobbing/Manevi Tazminat (TBK m.58)

### Aktüerya (5)
8. Destekten Yoksun Kalma (TRH 2010)
9. Maluliyet Tazminatı
10. Geçici İş Göremezlik (5510 s.K. m.18)
11. Bakıcı Gideri (TBK m.54)
12. Araç Değer Kaybı

### Faiz (5)
13. Yasal Faiz (3095 s.K. m.1)
14. Ticari Temerrüt Faizi (3095 s.K. m.2)
15. Akdî Temerrüt Faizi (TBK m.120)
16. Kira Artış Tespiti (TBK m.344)
17. Menfi Tespit Faizi (TBK m.78-79)

---

## Teknoloji Yığını

| Katman | Değer |
|---|---|
| Backend | ASP.NET Core MVC (.NET 10) |
| Veritabanı | MS SQL Server 2022 (LocalDB geliştirmede) |
| Cache | Redis (resilient — in-memory fallback) |
| ORM | Entity Framework Core 10 (Code First) |
| Auth | ASP.NET Core Identity + JWT (planlı) |
| Real-time | SignalR (Faz 4) |
| Background Jobs | Hangfire (Faz 3 — veri tazelik bildirimi için) |
| Logging | Serilog (Console + File + MS SQL Server) |
| Test | xUnit + FluentAssertions + Moq |
| CSS | Custom (modüler, BEM, design tokens) |
| Fontlar | Fraunces, Cormorant Garamond, JetBrains Mono |

---

## Proje Yapısı

```
LexCalculus/
├── LexCalculus.Web/              ASP.NET MVC (Controllers, Views, wwwroot)
├── LexCalculus.Core/             Domain entities, interfaces, calculator logic
├── LexCalculus.Infrastructure/   EF Core, repositories, external services
├── LexCalculus.Jobs/             Hangfire jobs (Faz 3'te aktive olacak)
├── LexCalculus.Tests/            Unit & integration testler
└── docs/                         Hukuki referanslar, roadmap, veri tazelik
    └── templates/                Görsel kimlik referans şablonları
```

---

## Tasarım Referansı

`docs/templates/bilirkisi-hesap.html` dosyası projenin **görsel kimlik kaynağıdır**. Tüm CSS Custom Properties (renk paleti, tipografi ölçeği, spacing scale), component dili (`tool-card`, `calc-header`, `form-section`, `result-panel`), ve dekoratif elementler (`❦` ornament, double gold border, italic burgundy `<em>` vurgu) bu dosyadan birebir çıkarılmıştır.

Yeni component eklerken bu dosyayı önce inceleyin. Tahmin ile değer atamak yerine kaynaktan örnek alın.

---

## Geliştirme Standartları

- **Inline `style="..."` kullanımı yasaktır.** Tüm stiller `wwwroot/css/02-components/` altındaki modüler dosyalarda yer alır. Tek seferlik özel değerler için bile component class'ı oluşturulur.
- **CSS Custom Properties zorunludur.** Hex/px/font-family değerleri sabit yazılmaz; `00-tokens/variables.css` üzerinden referans verilir.
- **BEM metodolojisi** ile class isimlendirmesi: `.block__element--modifier`.
- **Mobil-önce yaklaşım:** Default mobil layout, breakpoint 900px üzerinde desktop override.
- **EF Core'da raw SQL yasaktır.** Tüm sorgular LINQ veya `FromSqlInterpolated` (parametreli).
- **Audit timestamp'leri elle set edilmez.** `BaseEntity` türevlerinde `AuditInterceptor` halleder.
- **Repository SaveChanges çağırmaz.** Caller `IUnitOfWork.SaveChangesAsync` ile commit eder.

---

## Geliştirme Ortamı Kurulumu

### Gereksinimler

- .NET 10 SDK (10.0.202+)
- SQL Server 2022 LocalDB (Visual Studio ile birlikte gelir)
- Visual Studio 2026 veya VS Code + C# Dev Kit
- Git
- (Opsiyonel) Docker Desktop — Redis için
- (Opsiyonel) Node.js LTS — frontend tooling için

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

İlk çalıştırmada otomatik olarak migration uygulanır ve seed data yüklenir
(FormulaParameters, LifeTable, admin kullanıcısı, roller).

### Testleri çalıştırma

```bash
dotnet test
```

Beklenen: **219/219 pass**.

### Yeni migration ekleme

```bash
dotnet ef migrations add <MigrationAdı> --project LexCalculus.Infrastructure --startup-project LexCalculus.Web --output-dir Data/Migrations
```

---

## Health Check Endpoint'leri

| Endpoint | Amaç | Latency |
|---|---|---|
| `/health/live` | Process ayakta mı? (K8s liveness probe) | ~5 ms |
| `/health/ready` | Trafik kabul edebilir mi? DB + cache check (K8s readiness probe) | ~50-2000 ms |
| `/health` | Tüm check'ler — JSON detaylı | aynı |

Cache failure `Degraded` döner (200 OK), sadece DB failure `Unhealthy` (503).

---

## SEO

- Meta tag, Open Graph, Twitter Card, JSON-LD `SeoMetaViewComponent` ile her sayfada
- `/sitemap.xml` — sitemaps.org 0.9 schema, 1 saat cache (Faz 2 sonu: 23 URL)
- `/robots.txt` — `/Identity`, `/Admin`, `/api` Disallow

Per-sayfa SEO için controller içinde:

```csharp
ViewData["PageMeta"] = new SeoMeta { Title = "...", Description = "...", JsonLd = "..." };
```

---

## Dokümantasyon

| Doküman | İçerik |
|---|---|
| [docs/legal-references.md](docs/legal-references.md) | 17 calculator için hukuki dayanak, formül, parametre, basitleştirme, planlanan iyileştirme |
| [docs/phase-3-roadmap.md](docs/phase-3-roadmap.md) | Faz 3 detaylı planı (9 alt adım, riskler, hazır altyapı) |
| [docs/data-freshness.md](docs/data-freshness.md) | Parametre güncelleme takvimi, kaynaklar, sorumlu |

---

## Mimari Karar Notları

**Parametre mimarisi:** Tool-specific (`ToolSlug = '<calculator>'`) ve global
(`ToolSlug = '*'`) parametre ayrımı. Detay: `docs/legal-references.md`.

**Faiz hesabı altyapısı:** Yasal Faiz, Akdî Temerrüt ve Menfi Tespit aynı
`IInterestRateService.GetRatePeriodsAsync('yasal-faiz', ...)` çağrısını
paylaşır. Tek seed yeri, tek güncelleme noktası.

**CalculationHistory:** Logged-in kullanıcılar için her başarılı hesap
otomatik loglanır (anonymous skip). Faz 2'de write-only; Faz 3'te okuma UI'ı.

**TRH 2010:** Hazine Müsteşarlığı resmi PMF tablosu, 0-99 yaş erkek + kadın.
Versiyonlama destekli (TRH 2025 çıkarsa yeni LifeTable yan yana eklenir).

**AYM K.2025/164:** 3095 m.1'in sözleşmeden kaynaklanmayan borç ilişkilerinde
iptali (yürürlük 01.08.2026). Akdî Temerrüt ve Menfi Tespit calculator'larında
dinamik uyarı verilir.

**FlexibleDecimalModelBinder:** Türkçe ondalık ayraç (`,`) ve İngilizce (`.`) her
ikisini de kabul eden custom binder. Detay: `LexCalculus.Web/ModelBinders/`.

---

## Yol Haritası

| Faz | Konu | Durum |
|---|---|---|
| 1 | Temel altyapı | ✅ Tamamlandı |
| 2 | Hesaplama modülleri (İş Hukuku / Aktüerya / Faiz) | ✅ Tamamlandı |
| 3 | Yönetim paneli + hesap geçmişi UI + multi-tenant | Sonraki |
| 4 | Sosyal platform (profil, mesajlaşma, SignalR) | Planlı |
| 5 | Otomatik veri çekme + kalan kategoriler (D-I) | Planlı |

Detay: [docs/phase-3-roadmap.md](docs/phase-3-roadmap.md)

---

## Lisans

Proprietary. © 2026 Lex Calculus. Tüm hakları saklıdır.

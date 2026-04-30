# Lex Calculus

Türk hukukuna özgü hesaplamalar için profesyonel SaaS platformu. Avukatlar ve bilirkişiler için tasarlanmış.

## Durum

**Faz 3 — Admin Paneli + Multi-tenant + Audit tamamlandı (Nisan 2026).**

- 17/17 hesaplayıcı aktif
- 396/396 test geçiyor
- Admin paneli: parametre/LifeTable/kullanıcı/tenant/talep/davet CRUD, activity log
- Multi-tenant altyapı (hukuk büroları); bireysel vatandaş kullanıcıları etkilenmiyor (TenantId nullable)
- 2 Hangfire recurring job (veri tazelik kontrolü, davet expire)
- 9 hukuk kategorisinden 3'ü (İş Hukuku, Aktüerya, Faiz) hesaplayıcı olarak hazır
- Kalan 6 kategori (Vergi, Aile, Miras, vb.) Faz 5'te eklenecek

## Hesaplayıcılar

### İş Hukuku (7/7)
- Kıdem Tazminatı (1475 s.K. m.14)
- İhbar Tazminatı (4857 s.K. m.17)
- Yıllık İzin Ücreti (4857 s.K. m.53)
- Fazla Mesai Alacağı (4857 s.K. m.41)
- İşe İade Tazminatı (4857 s.K. m.18-21)
- Asgari Ücret Uyumluluk Kontrolü (4857 s.K.)
- Mobbing / Manevi Tazminat (TBK m.58 + 4857 s.K. m.5,77)

### Aktüerya (5/5)
- Destekten Yoksun Kalma (TBK m.53 + Yargıtay HGK)
- Maluliyet Tazminatı (TBK m.54 + 5510 s.K.)
- Geçici İş Göremezlik (5510 s.K. m.18 / TBK m.54)
- Bakıcı Gideri (TBK m.54)
- Araç Değer Kaybı (TRAMER baseline)

### Faiz (5/5)
- Yasal Faiz (3095 s.K. m.1)
- Ticari Temerrüt Faizi (3095 s.K. m.2)
- Akdî Temerrüt Faizi (TBK m.120)
- Kira Artışı (TBK m.344)
- Menfi Tespit Faizi (TBK m.78-79)

## Mimari

ASP.NET Core MVC (.NET 10), 5 katmanlı solution:
- **LexCalculus.Core** — Domain entities, calculator interfaces, DTO'lar
- **LexCalculus.Infrastructure** — EF Core, services, repositories, calculator implementasyonları
- **LexCalculus.Web** — MVC controllers, views, statik kaynaklar
- **LexCalculus.Tests** — xUnit testleri (219/219)
- **LexCalculus.Shared** — Cross-cutting tipler

### Önemli mimari kararlar

- **Identity:** ASP.NET Core Identity, `ApplicationUser : IdentityUser<int>`. Audit interceptor ile CreatedAt/UpdatedAt/DeletedAt otomatik.
- **Soft delete:** `IsDeleted` query filter; veri kaybı önlenir.
- **Calculator pattern:** Her hesaplayıcı `ICalculator<TInput, TResult>` implementasyonu, DI ile registry'ye otomatik kayıt.
- **Parametre yönetimi:** `FormulaParameter` tablosu, `ToolSlug + Key + EffectiveDate` ile zaman bazlı oran/tavan saklar. `*` ToolSlug = global parametre konvansiyonu (örn. damga vergisi, yasal faiz).
- **Caching:** Redis (resilient, in-memory fallback). FormulaParameter ve LifeTable cache'lenir.
- **Aktüeryal hesaplar:** TRH 2010 yaşam tablosu (Hazine Müsteşarlığı, 0-99 yaş erkek/kadın), `IActuarialService.AnnuityPresentValue` ile peşin değer.
- **Calculation history:** Write-only Faz 2; payload guard'lı (200KB input, 500KB output), anonim kullanıcı atlar, hata yutar (try/catch).
- **Decimal binding:** Custom `FlexibleDecimalModelBinder` virgül ve nokta ondalık ayraçlarını kabul eder; `AllowThousands` flag'i kapalı (sessiz hata önleme).

## UX

- 17/17 calculator iki kutulu pattern: lacivert "Bu hesap ne zaman kullanılır?" (info-box--info) + sarı uyarı (info-box--warning)
- 9-renkli özel palet (Fraunces, Cormorant Garamond, JetBrains Mono fontları)
- Modüler CSS: 5 katmanlı (00-foundations → 04-pages)
- Sitemap + robots.txt + per-page SEO meta

## Dokümantasyon

- `docs/legal-references.md` — 17 hesaplayıcının hukuki temelleri ve formülleri
- `docs/data-freshness.md` — Parametre güncellik tablosu
- `docs/phase-3-roadmap.md` — Faz 3 plan (admin paneli, Hangfire, bildirim sistemi)

## Önemli Dokümanlar

| Dosya | İçerik |
|---|---|
| `CLAUDE.md` | Claude Code için kalıcı bağlam ve kurallar |
| `docs/operations.md` | Operasyon runbook (cache, Hangfire, e-posta) |
| `docs/tech-debt.md` | Bilinçli ertelenen mimari kararlar |

Yeni bir mimari sapma yapıldığında `docs/tech-debt.md`'ye 4-başlıklı
format ile not düşün. Faz sonlarında bu defter gözden geçirilir.

## Kurulum

```bash
git clone <repo>
cd LexCalculus
dotnet restore
dotnet ef database update --project LexCalculus.Infrastructure --startup-project LexCalculus.Web
dotnet run --project LexCalculus.Web --launch-profile https
```

LocalDB instance: `(localdb)\MSSQLLocalDB`, db: `LexCalculusDb`.
Default admin: `admin@lexcalculus.local` (parola seed sırasında oluşturulur).

## Faz 2 sonu metrikler

| Metrik | Değer |
|---|---|
| Hesaplayıcı | 17/17 |
| Test | 219/219 |
| DB tablo | 13 |
| FormulaParameter | ~95 satır |
| Lifetable satırı | 200 (TRH 2010, 0-99 erkek/kadın) |
| Migration | 22 |
| Adım | 25/25 |

## Faz 3 — Admin Paneli + Multi-tenant + Audit (Tamamlandı: 2026-04-30)

Faz 3, sistemi tek kullanıcılı ortamdan çoklu kullanıcı + organizasyonel SaaS'a taşıdı.
Detay: `docs/phase-3-roadmap.md`.

### Adımlar

| Adım | Konu | Durum |
|------|------|-------|
| 3.1  | Admin Layout & Authorization Policy   | ✅ |
| 3.2  | FormulaParameter CRUD admin paneli    | ✅ |
| 3.3  | Veri Tazelik Bildirim Sistemi (6 parça) | ✅ |
| 3.4  | Hesap Geçmişi UI (3 parça)            | ✅ |
| 3.5  | LifeTable CRUD admin paneli           | ✅ |
| 3.6  | Kullanıcı & Rol Yönetimi (4 parça)    | ✅ |
| 3.7  | Multi-tenant altyapı (5 parça)        | ✅ |
| 3.8  | Activity Log + ExpireInvitationsJob (2 parça) | ✅ |
| 3.9  | Faz 3 cleanup + closeout (2 parça)    | ✅ |

### Faz 3 sonu metrikler

| Metrik | Değer |
|---|---|
| Test | 396/396 |
| Yeni test (Faz 3) | +70 (326 → 396) |
| Yeni migration | 6 |
| Hangfire recurring job | 2 (DataFreshnessCheck, ExpireInvitations) |
| Yeni admin sayfa | parametre, life-table, kullanıcı, tenant, talep, davet, activity-log |
| Tech-debt kapatıldı | 5 madde |
| Tech-debt açık (Faz 4'e) | 4 madde |

### Mimari kararlar (öne çıkanlar)

- **Multi-tenant:** TenantId nullable pattern; bireysel vatandaş kullanıcılar UI fark görmez
- **Plan/üyelik field YOK** — sistem ücretsiz, vatandaş odaklı vizyon
- **ShareWithTenant Core/Input DTO'larına SIZDIRILMADI** (controller seviyesi)
- **AsAdminQuery()** niyet açıklayıcı bypass extension (IgnoreQueryFilters wrapper)
- **ActivityLog:** service-level manuel log, HttpContext null'da defansif (background job uyumlu)
- **Davet kabulü:** Identity scaffolded register sayfası DEĞİŞTİRİLMEDİ, ayrı `/davet/kayit` flow

### Açık tech-debt (Faz 4)

- Madde 1: Hangfire bağımlılığı Infrastructure'a sızdı
- Madde 3: EF Core migration default uyarısı (kalıcı izleme)
- Madde 4: Hangfire 401/403 (düşük öncelik, teorik)
- Madde 9: ActivityLog retention policy (KVKK/hukuk dış bağımlılık)

## Faz 4 — Sosyal Platform (Devam Ediyor)

**Başlangıç:** 30 Nisan 2026
**Tahmini süre:** ~14 hafta (3 dalga, 14 alt adım)
**Charter:** [docs/phase-4-charter.md](./docs/phase-4-charter.md)
**Roadmap:** [docs/phase-4-roadmap.md](./docs/phase-4-roadmap.md)

Sosyal katman: profil, bağlantı, engelleme, kullanıcı içerik üretimi
(makale + yorum + beğeni + şikayet + moderasyon), real-time mesajlaşma.

Vizyon ilkeleri:
- Plansız (ücretsiz, herkes)
- Vatandaş 1. sınıf vatandaş
- Tenant ↔ sosyal bağımsız
- Default gizli, opt-in açık

---

LexCalculus, Türk hukukunun karmaşık hesaplamalarını şeffaf ve denetlenebilir kılmak için tasarlandı.

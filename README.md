# Lex Calculus

Türk hukukuna özgü hesaplamalar için profesyonel SaaS platformu. Avukatlar ve bilirkişiler için tasarlanmış.

## Durum

**Faz 2 — Hesaplama Modülleri tamamlandı (Nisan 2026).**

- 17/17 hesaplayıcı aktif
- 219/219 test geçiyor
- 9 hukuk kategorisinden 3'ü (İş Hukuku, Aktüerya, Faiz) Faz 2 kapsamında bitirildi
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

## Sonraki: Faz 3

Admin paneli: parametre CRUD, veri tazelik bildirim sistemi (Hangfire + e-posta), hesap geçmişi UI, kullanıcı yönetimi, multi-tenant. Detay: `docs/phase-3-roadmap.md`.

---

LexCalculus, Türk hukukunun karmaşık hesaplamalarını şeffaf ve denetlenebilir kılmak için tasarlandı.

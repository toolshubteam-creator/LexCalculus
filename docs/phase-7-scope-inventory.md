# Faz 7 Kapsam Envanteri (Denetim Raporu — Adım 7.0)

> **Geçici denetim raporu.** Faz 7 charter'ından (Adım 7.1) ÖNCE yapılan
> "hesaplama altyapısı ne hazır, ne eksik" denetimi. Charter DEĞİL: yeni kod
> yok, yeni calculator yok, yeni tag yok. Amaç: Faz 2 hesaplama altyapısını
> envanterlemek + kalan D-I araçlarını kesin sayıyla kapsama almak. Charter
> (7.1) bu envanteri öncelik ve dalga temeli olarak kullanacak.
>
> Tarih: 4 Haziran 2026 · Son commit: `24d7f96` · Son tag: `phase-6-complete`
> Temel doğrulama: **dosya sistemi** (konuşma context'i değil — Faz 6 #41 öğrenmesi)

---

## §1 Faz 6 sonu durum

| Metrik | Değer |
|---|---|
| Tag | `phase-6-complete` (2 Haziran 2026) |
| Test | 838 yeşil (regresyon 0) |
| Build | 0 hata · 0 uyarı (warnaserror korundu) |
| Migration | 34 |
| Hesaplayıcı | **17/17 aktif** (9 hukuk kategorisinden 3'ü hazır: A+B+C) |
| TargetFramework | .NET 10 |

**17 aktif calculator** (`CalculatorServiceCollectionExtensions.AddCalculators`):

- **A — İş Hukuku (7):** Kıdem Tazminatı, İhbar Tazminatı, Yıllık İzin, Fazla
  Mesai, İşe İade, Asgari Ücret Kontrolü, Mobbing
- **B — Aktüerya (5):** Destekten Yoksun Kalma, Maluliyet, Geçici İş
  Göremezlik, Bakıcı Gideri, Araç Değer Kaybı
- **C — Faiz (5):** Yasal Faiz, Ticari Temerrüt Faizi, Akdî Temerrüt Faizi,
  Kira Artışı, Menfi Tespit Faizi

Faz 6 (olgunlaştırma + UX + performans) hesaplama modüllerine **dokunmadı**;
A/B/C araçları Faz 2'den beri stabil. Faz 7 = **D-I kategorilerinin tamamlanması**.

---

## §2 Faz 2 altyapısı (reuse edilecek — dokunulmaz)

Tüm dosya yolları fiilen okundu. Faz 7'de yeni araç eklerken **bu pattern'ler
aynen tekrarlanır**; altyapıya yeni kod yazılması gerekmez.

### 2a. ICalculator pattern

- `LexCalculus.Core/Calculators/Common/ICalculator.cs` — marker `ICalculator`
  (sadece `Metadata`) + generic `ICalculator<TInput,TResult>` (`CalculateAsync`).
  **Sözleşme:** pure (aynı girdi+parametre+EffectiveDate → aynı sonuç), yan
  etkisiz (DB write yok), invalid input'ta throw değil `ValidationErrors` döner,
  async sadece parametre yüklemesi DB roundtrip olduğu için.
- `CalculatorMetadata.cs` — alanlar: `Slug`, `Category`, `Title`,
  `ShortDescription`, `LegalReference`, `Status` (default Active), `Keywords`,
  `DisplayNumber`; türetilmiş `UrlPath => /hesapla/{category-slug}/{slug}`.
- `CalculatorCategory.cs` — **9 kategori enum HAZIR** (IsHukuku=1 … Bilirkisi=9).
  D-I (Gayrimenkul, AileMiras, Ceza, VergiIdare, Ticaret, Bilirkisi) zaten
  tanımlı; slug/display/short adları `CalculatorCategoryExtensions.cs`'te
  eksiksiz (ToSlug/ToDisplayName/ToShortName/FromSlug). **Faz 7'de enum'a
  dokunulmaz** — sadece her kategoriye calculator eklenir.
  > Not: enum XML comment'leri D-I'yı "Faz 5" diyor (doküman drift'i — gerçekte
  > Faz 7). Kozmetik; kod davranışı etkilenmez. Charter'da düzeltilebilir.
- `CalculatorStatus.cs` — Active / Beta / ComingSoon / Deprecated.
- `ICalculatorRegistry` + `Infrastructure/Calculators/CalculatorRegistry.cs` —
  eager singleton; `IEnumerable<ICalculator>` DI'dan toplanır, metadata snapshot
  immutable. Slug **benzersizlik kontrolü** var (duplicate slug → exception).
  API: `GetAll`, `GetByCategory`, `FindBySlug`, `Find`, `GetActiveCategories`,
  `HasActiveTools`.

**Yeni araç ekleme reçetesi (3 dosya + 1 satır + 1 view + test):**
1. `Core/Calculators/{Kategori}/{Ad}Input.cs` + `{Ad}Result.cs` (Result →
   `CalculationResult` taban: `TotalAmount/TotalLabel/Unit/Rows/Warnings/Note/
   ValidationErrors/IsValid`).
2. `Core/Calculators/{Kategori}/{Ad}Calculator.cs` — `ICalculator<TIn,TOut>`,
   `Metadata` static init, `CalculateAsync` (önce validation, sonra hesap).
3. `Web/Extensions/CalculatorServiceCollectionExtensions.cs`'e iki `AddScoped`
   satırı (marker + generic).
4. `Web/Views/Hesapla/{Kategori}/{Ad}.cshtml` (§2d pattern).
5. `Tests/Calculators/{Ad}CalculatorTests.cs` (§2e pattern).

### 2b. FormulaParameter altyapısı

- `Core/Entities/Calculators/FormulaParameter.cs` — zaman-versiyonlu parametre.
  Alanlar: `ToolSlug` (`"*"` = global/cross-tool), `Key`, `Value` (decimal),
  `EffectiveDate`, `IsAutoUpdated`, `Source`, `Note`, `ExpectedUpdateFrequency`,
  `LastUpdatedDate`, `Notes`, `CreatedByUserId`, `LastModifiedByUserId`.
- **Lookup semantiği:** `(toolSlug, key, asOfDate)` → `EffectiveDate <= asOfDate`
  olan **en güncel** satır. Geçmiş tarihli hesap (retroaktif) bu sayede çalışır.
  Tool-specific bulunamazsa `"*"` global'e fallback eder.
- `Infrastructure/Calculators/FormulaParameterService.cs` — `IFormulaParameter
  Service`. `GetValueAsync`/`GetParameterAsync` (24 saat distributed cache,
  index-key invalidation), `GetHistoryAsync`, `AddAsync`, `UpdateAsync`,
  `SoftDeleteAsync`, `ExistsAsync`, `GetAllAsync`. ActivityLog entegre.
- Seeder: `Infrastructure/Data/SeedData/CalculatorParameterSeeder.cs` — statik
  liste, idempotent (`ToolSlug+Key+EffectiveDate` ile eşleşen atlanır). Yeni
  araç parametreleri buraya eklenir.

### 2c. CalculationHistory

- `Core/Entities/CalculationHistory.cs` — `UserId` (zorunlu, >0; anonim
  loglanmaz), `CategorySlug`, `ToolSlug`, `ToolTitle`, `InputJson`, `OutputJson`,
  `TotalAmount?`, `Unit?`, `UserLabel?`, `CaseReference?`, `TenantId?` (Faz 3.7
  opt-in paylaşım — global query filter ile zorlanır). Soft-delete (BaseEntity).
- `Infrastructure/Services/CalculationHistoryService.cs` (DI'da kayıtlı).
  **Yeni araçlar otomatik kapsanır** — geçmiş kaydı slug-agnostic.

### 2d. View pattern (`/Views/Hesapla/{Kategori}/{Tool}.cshtml`)

Referans: `IsHukuku/KidemTazminati.cshtml`. Standart iskelet:
- `@model {Ad}Input`; `ViewData["Meta"]` (CalculatorMetadata) + `ViewData
  ["Result"]` (nullable result).
- `.calc-header` (Title + № DisplayNumber + LegalReference + kategori kısa ad).
- **info-box ikilisi:** `.info-box--info` ("Bu hesap ne zaman kullanılır?") +
  `.info-box--warning` (mevzuat/vergi uyarısı). `--danger` validation summary.
- `.calc-layout` → sol `form.form-section` (asp-for + asp-validation), sağ
  `.result-panel` (boş durum VEYA `.result-total` + `.result-rows` foreach +
  `.result-note` warnings/Note).
- `_PaylasimToggle` partial (tenant paylaşım), `_ValidationScriptsPartial`.
- **Inline style YASAK** (CLAUDE.md) — tüm CSS `wwwroot/css/` BEM.

### 2e. Test pattern (xUnit + FluentAssertions)

Referans: `Tests/Calculators/KidemTazminatiCalculatorTests.cs`.
- `SqlServerTestBase` (SQL Server LocalDB — Faz 5 Adım 5.8; InMemory değil).
- `Build()` helper: `_db.Create()` ctx, `FormulaParameter` seed, gerçek
  `FormulaParameterService` (`MemoryDistributedCache` + `NullActivityLogService`
  + `NullLogger`), calculator inşa.
- **Zorunlu minimum (CLAUDE.md):** ≥1 happy path + ≥1 edge case. Mevcut örnek 6
  test: referans mevzuat değeri (`BeApproximately`), eşik/tavan davranışı,
  validation hataları (2), opsiyonel hesap, eksik parametre exception.
- **Türkçe encoding kuralı** (CLAUDE.md / [[feedback_test_razor_encoding]]):
  view assertion'larında ASCII-stable substring kullan.

### 2f. Sitemap entegrasyonu — **otomatik**

`Infrastructure/Seo/DefaultSitemapBuilder.cs`: `_registry.GetActiveCategories()`
→ kategori URL'leri + `_registry.GetAll().Where(Status==Active)` → araç
URL'leri. **Yeni bir D-I calculator `Status=Active` ile register edilince
sitemap'e + kategori landing'e otomatik girer** — manuel adım yok. ComingSoon/
Deprecated araçlar sitemap dışı.

---

## §3 Mevcut placeholder durumu — **BOŞ (beklenmedik bulgu)**

`Core/Calculators/PlaceholderCalculators.cs` **fiilen okundu**:

- Dosyada **hiçbir somut placeholder kalmamış**. Sadece soyut `Placeholder
  Calculator : ICalculator` taban sınıfı + 17 adet `// Note: X moved to real
  implementation in ...` yorum satırı var.
- Faz 2'de 17 placeholder'ın tamamı gerçek implementasyona taşınmış; D-I
  kategorileri için **hiç placeholder yazılmamış**.

> **⚠ Süreç notu:** Konuşma context'i "placeholder'lar kalan araçları tutuyor"
> varsayıyordu — **dosya sistemi bunu çürüttü**. D-I araçları ne placeholder ne
> registry'de; sıfırdan yazılacaklar. Bu, #41 (kullanım taraması zorunlu)
> öğrenmesinin neden gerekli olduğunun somut kanıtı: hatırlanan varsayımla
> değil, dosyayla doğrulandı.

**Registry kaydı (`AddCalculators`):** 17 marker + 17 generic `AddScoped`
(toplam 34 satır) + `ITUFEService`, `ICalculationHistoryService`, singleton
`ICalculatorRegistry`. Aktif=17, placeholder=0, ComingSoon=0.

---

## §4 Teknik rapor §6 vs Ek A tutarsızlık çözümü — **ÇÖZÜLDÜ**

### Kaynak belgenin durumu

Teknik rapor (`LexCalculus_TeknikRapor.docx`) **repo working tree'sinde yoktu**.
Dangling commit `a90edc6` ("Add files via upload", 3 Haziran 2026) içinde blob
olarak duruyordu ama **`main` HEAD geçmişine merge edilmemişti** — bu yüzden
hiçbir dosya taraması bulamadı. Blob çıkarıldı (`git show a90edc6:...`), XML'den
metne dönüştürülüp **fiilen okundu** ve bu denetimle birlikte
`docs/templates/LexCalculus_TeknikRapor.docx` konumuna **kalıcı eklendi**
(CLAUDE.md'nin beklediği yol). Blob hash doğrulandı: `c7ad58f…` (byte-identik).

### Sayım (rapordan birebir)

| Kaynak | A | B | C | D | E | F | G | H | I | Toplam | D-I |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **§6 Tam Katalog** (detaylı) | 7 | 5 | 5 | 5 | 4 | 5 | 5 | 3 | 4 | **43** | **26** |
| **Ek A** (özet liste) | 6 | 4 | 3 | 3 | 4 | 3 | 4 | 2 | 3 | **32** | 19 |

### Çözüm: §6 otoritedir, Ek A eksiktir

Ek A, §6'ya göre **11 aracı düşürmüş** — ve bunların **4'ü zaten ÜRETİMDE olan**
araçlar:

- **Ek A'da olmayan ama BUILT olan 4 araç:** A6 Mobbing, B4 Bakıcı Gideri,
  C3 Akdî/Temerrüt Faizi, C5 Menfi Tespit Faizi.
- **Ek A'da olmayan 7 D-I aracı:** D4 Kat Karşılığı, D5 Hâsılat Kira, F1 Ceza
  Erteleme, F5 Tutukluluk Mahsup, G4 KDV İadesi, H3 Sözleşme Cezası, I4
  Çevresel Zarar.

Ek A halihazırda canlı 4 aracı bile atladığına göre **bir öncelik/öneri listesi
değil, kusurlu bir özettir.** §6 başlığı zaten "**Tam Katalog**" — otorite budur.

**KESİN KARAR — Faz 7 kapsamı = §6'nın D-I'sı = 26 yeni araç (toplam 43).**

- Mevcut 17 (A+B+C) → §6 ile birebir örtüşür (doğrulandı).
- Charter belgelerinin tekrarladığı **"15 yeni hesaplayıcı"** (phase-4/5/6
  charter) → §6 kataloğu kesinleşmeden önceki **kaba erken tahmin**; §6'nın
  26'sıyla geçersiz kaldı. Charter (7.1) bu sayıyı 26'ya günceller.
- Konuşma context'indeki "26" → **§6 ile doğrulandı, doğruydu.** (Tesadüfen
  değil — §6 detay kataloğundan geliyordu.)

---

## §5 Faz 7 kapsamı — 26 yeni araç (§6 Tam Katalog D-I)

Slug'lar mevcut konvansiyonla (kebab-case) önerildi; charter/implementasyonda
kesinleşir. Karmaşıklık: **BASİT** (tek formül, sabit/girdi parametre) ·
**ORTA** (çoklu girdi, koşullu mantık, dönemsel parametre) · **KARMAŞIK** (yeni
altyapı gerektirir). Parametre: **HAZIR** (mevcut servis/parametre yeter) ·
**YENİ-PARAMETRE** (yeni FormulaParameter satırı) · **YENİ-TABLO/ALTYAPI** (yeni
veri yapısı veya servis).

### D — Gayrimenkul ve Kat Mülkiyeti (`gayrimenkul`) — 5

| # | Slug | Türkçe Ad | Mevzuat | Parametre | Karmaşıklık |
|---|---|---|---|---|---|
| D1 | `arsa-payi` | Arsa Payı Hesabı | 634 s.K. m.3 | HAZIR (katsayı in-code) | ORTA |
| D2 | `kamulastirma-bedeli` | Kamulaştırma Bedeli | 2942 s.K. | YENİ-PARAMETRE (kapitalizasyon/kira çarpanı) | KARMAŞIK |
| D3 | `ecrimisil` | Ecrimisil | TMK/HMK | HAZIR (faiz altyapısı reuse) | ORTA |
| D4 | `kat-karsiligi-insaat` | Kat Karşılığı İnşaat Paylaşımı | TBK genel | HAZIR (girdi-bazlı) | ORTA |
| D5 | `hasilat-kira` | Hâsılat Kira Hesabı | TBK ticari kira | HAZIR (girdi-bazlı) | BASİT |

### E — Aile ve Miras Hukuku (`aile-miras`) — 4

| # | Slug | Türkçe Ad | Mevzuat | Parametre | Karmaşıklık |
|---|---|---|---|---|---|
| E1 | `nafaka` | Nafaka (Tedbir/Yoksulluk/İştirak) | TMK m.182/197/364 | HAZIR (ITUFEService reuse — TÜFE artış) | ORTA |
| E2 | `miras-payi` | Miras Payı Hesabı | TMK m.495-501 | YENİ-ALTYAPI (mirasçılık dereceleri + saklı pay mantığı) | KARMAŞIK |
| E3 | `tenkis` | Tenkis Hesabı | TMK m.560 | YENİ-ALTYAPI (E2 saklı pay altyapısına bağımlı) | KARMAŞIK |
| E4 | `mal-rejimi-tasfiyesi` | Mal Rejimi Tasfiyesi | TMK m.218 vd. | HAZIR (girdi-bazlı, edinilmiş/kişisel ayrım) | ORTA |

### F — Ceza Hukuku ve İnfaz (`ceza`) — 5

| # | Slug | Türkçe Ad | Mevzuat | Parametre | Karmaşıklık |
|---|---|---|---|---|---|
| F1 | `ceza-erteleme` | Ceza Erteleme Süresi | 5237 s.K. m.51 | YENİ-ALTYAPI (ceza takvim: gün/ay/yıl) | ORTA |
| F2 | `kosullu-saliverilme` | Koşullu Salıverilme Tarihi | 5275 s.K. m.107 | YENİ-ALTYAPI (takvim + tahliye tarihi) | KARMAŞIK |
| F3 | `dava-zamanasimi` | Dava Zamanaşımı | 5237 s.K. m.66-67 | YENİ-PARAMETRE (suç tipi süre tablosu) + takvim | KARMAŞIK |
| F4 | `adli-para-cezasi` | Adli Para Cezası | 5237 s.K. m.52 | HAZIR (gün × günlük tutar) | BASİT |
| F5 | `tutukluluk-mahsup` | Tutukluluk Mahsup | CMK / 5275 s.K. | YENİ-ALTYAPI (takvim/süre mahsup) | ORTA |

### G — Vergi ve İdare (`vergi-idare`) — 5

| # | Slug | Türkçe Ad | Mevzuat | Parametre | Karmaşıklık |
|---|---|---|---|---|---|
| G1 | `veraset-vergisi` | Veraset ve İntikal Vergisi | 7338 s.K. | YENİ-TABLO (artan oranlı vergi dilimi + istisna) | KARMAŞIK |
| G2 | `tapu-harci` | Tapu Harcı | 492 s.K. | YENİ-PARAMETRE (harç oranı + döner sermaye) | BASİT |
| G3 | `damga-vergisi` | Damga Vergisi | 488 s.K. | YENİ-PARAMETRE (sözleşme türü oranı + azami sınır) | ORTA |
| G4 | `kdv-iadesi` | KDV İadesi Alacağı | 3065 s.K. m.29 | HAZIR/YENİ-PARAMETRE (indirimli oran) | ORTA |
| G5 | `vergi-cezasi` | Vergi Cezası ve Gecikme Faizi | 213 s.K. | YENİ-PARAMETRE (gecikme zammı aylık oran) | ORTA |

### H — Ticaret Hukuku (`ticaret`) — 3

| # | Slug | Türkçe Ad | Mevzuat | Parametre | Karmaşıklık |
|---|---|---|---|---|---|
| H1 | `sirket-tasfiye-payi` | Şirket Tasfiye Payı | 6102 s.K. TTK | HAZIR (girdi-bazlı bilanço) | ORTA |
| H2 | `kar-payi-dagitimi` | Anonim Şirket Kâr Payı | 6102 s.K. TTK | HAZIR (1./2. temettü kuralları) | ORTA |
| H3 | `sozlesme-cezasi` | Tazminat Sözleşme Cezası | TBK m.179-182 | HAZIR (ceza şartı + hâkim indirimi) | BASİT |

### I — Bilirkişilik Özel Araçları (`bilirkisi`) — 4

| # | Slug | Türkçe Ad | Mevzuat | Parametre | Karmaşıklık |
|---|---|---|---|---|---|
| I1 | `pmf-yasam-tablosu` | PMF Yaşam Tablosu Sorgulama | TRH 2010 | **HAZIR** (ILifeTableService reuse) | BASİT |
| I2 | `iskontolu-nakit-akisi` | İskontolu Nakit Akışı | Finans matematiği | **HAZIR** (IActuarialService annuity/discount reuse) | BASİT |
| I3 | `hakkaniyet-tazminat` | Hakkaniyetli Tazminat Simülatörü | Emsal kararlar | YENİ-ALTYAPI (emsal veri) veya ORTA (girdi-bazlı tahmin) | KARMAŞIK |
| I4 | `cevresel-zarar` | Çevresel Zarar Tazminatı | 2872 s.K. | YENİ-PARAMETRE (kirlilik türü katsayısı) | ORTA |

**Toplam: 26 araç** — D5 + E4 + F5 + G5 + H3 + I4.
Karmaşıklık dağılımı (tahmini): BASİT 5 · ORTA 12 · KARMAŞIK 9.

---

## §6 Dalga kırılımı önerisi (bağlayıcı değil — charter 7.1 kesinleştirir)

Faz 6 üç-dalga pattern'i. Adım numaralandırma: 7.0 (bu envanter), 7.1 (charter),
7.2-7.13.

### Dalga A — Gayrimenkul + Aile/Miras (D + E, 9 araç)
- D1-D5 (5) + E1-E4 (4). En çok **HAZIR/reuse** araç burada (TÜFE, faiz altyapısı).
- En ağır AR-GE: E2 Miras Payı + E3 Tenkis (mirasçılık dereceleri + saklı pay).
- Öneri adımlar: 7.2 (D altyapı + D1-D3), 7.3 (D4-D5), 7.4 (E1+E4), 7.5 (E2 miras
  altyapısı + E2), 7.6 (E3 tenkis + Dalga A closeout). Tahmin: ~5-6 gün.

### Dalga B — Ceza + Vergi/İdare (F + G, 10 araç)
- F1-F5 (5) + G1-G5 (5). **İki yeni altyapı** burada doğar:
  **ceza takvim altyapısı** (F1/F2/F3/F5) + **vergi dilim/oran altyapısı**
  (G1 veraset dilimleri, G2/G3/G5 harç-oran parametreleri).
- Öneri adımlar: 7.7 (ceza takvim altyapısı + F1/F4), 7.8 (F2/F3/F5), 7.9 (vergi
  dilim altyapısı + G1), 7.10 (G2-G5 + Dalga B closeout). Tahmin: ~5-6 gün.

### Dalga C — Ticaret + Bilirkişi + closeout (H + I, 7 araç)
- H1-H3 (3) + I1-I4 (4). I1/I2 **doğrudan reuse** (LifeTable/Actuarial) → hızlı.
- I3 Hakkaniyet en belirsiz (emsal veri kararı charter'da netleşir).
- Öneri adımlar: 7.11 (H1-H3), 7.12 (I1-I4), 7.13 (Faz 7 closeout: roadmap,
  README, tech-debt, tag `phase-7-complete`). Tahmin: ~3-4 gün.

**Toplam tahmin: ~13-16 gün** (Faz 6 baseline). **Muhafazakâr baseline notu:**
D-I yeni hukuk alanı + 9 KARMAŞIK araç + 3 yeni altyapı → A/B/C reuse avantajı
sınırlı; gerçek süre daha uzun olabilir. Her dalga sonunda yeniden tahmin.

---

## §7 Yeni altyapı ihtiyaçları

Faz 7'nin A/B/C'den farkı: bazı araçlar **yeni veri yapısı/servis** gerektirir.
Charter'da bu altyapıların her biri kendi ön-adımına ayrılmalı.

1. **Vergi dilim tablosu** (G1 Veraset, kısmen G4 KDV) — artan oranlı matrah
   dilimleri. FormulaParameter tek `Value` decimal taşır; dilim tablosu için ya
   çoklu Key konvansiyonu (`veraset-dilim-1-alt`, `-ust`, `-oran` …) ya da yeni
   `TaxBracket` entity gerekir. **Karar charter'da.**
2. **Ceza takvim altyapısı** (F1/F2/F3/F5) — gün/ay/yıl ekleme, tahliye/
   zamanaşımı tarihi, durma/kesilme. Ortak `IPenalCalendarService` veya saf
   `DateTime` helper. 4 araç paylaşır → bir kez yazılır.
3. **Miras/Tenkis altyapısı** (E2/E3) — yasal mirasçı dereceleri (TMK m.495-501)
   + saklı pay oranları. E3 tenkis E2'ye bağımlı; E2 önce.
4. **Harç/oran parametreleri** (G2 tapu, G3 damga azami + sözleşme türü, G5
   gecikme zammı) — yeni FormulaParameter satırları (yeni tablo değil) + seeder.
5. **Emsal karar verisi** (I3 Hakkaniyet) — en belirsiz; charter girdi-bazlı
   tahmine indirgeyebilir veya Faz 8'e erteleyebilir.

**Yeniden kullanılan (yeni altyapı YOK):** ILifeTableService (I1, E-aktüer),
IActuarialService annuity/discount (I2, D2 değerleme), ITUFEService (E1 nafaka,
D3 ecrimisil), IInterestRateService/3095 (D3, F4), FormulaParameter zaman-
versiyonlu pattern (tümü), CalculationHistory (tümü, otomatik), sitemap (otomatik).

---

## §8 Süreç notları (Faz 6 #41 öğrenmesi uygulandı)

- **#41 kullanım taraması zorunlu** — bu adımda fiilen uygulandı: PlaceholderCal
  culators.cs / AddCalculators / registry / 9-kategori enum **dosyadan** okundu,
  hatırlamayla değil.
- **Beklenmedik bulgu 1:** Placeholder dosyası BOŞ — D-I araçları sıfırdan
  yazılacak (placeholder reuse yok). §3'te raporlandı.
- **Beklenmedik bulgu 2:** Teknik rapor `main`'de yoktu (dangling commit
  `a90edc6`). Blob çıkarıldı, okundu, `docs/templates/`'e kalıcı eklendi. §4.
- **Tutarsızlık çözüldü:** §6 (43, Tam Katalog) vs Ek A (32, eksik özet). >5
  araç farkı (11) → karar noktası olarak kullanıcıya raporlandı; kullanıcı
  kaynağı (teknik rapor) sağladı; §6 otorite, **26 yeni araç** kesinleşti. §4.
- **Charter'a yanlış varsayım eklenmedi:** "15" charter figürünün §6 ile
  geçersiz olduğu tespit edildi; charter 7.1 26'ya güncelleyecek.

> **Yeni tech-debt adayı (#42 — charter 7.1'de deftere alınmalı):** Teknik rapor
> sadece dangling commit'te duruyordu; `main`'e hiç merge edilmemişti. Bu adımda
> `docs/templates/`'e eklendi ama **kök sebep** (binary upload'un branch'e
> bağlanmaması) bir süreç açığı. İleride referans belgeler doğrudan main'e
> commit'lenmeli. Ayrıca CalculatorCategory enum comment drift'i (D-I "Faz 5"
> diyor) düzeltilebilir.

---

## §9 Sonraki adım

**Adım 7.1: Faz 7 charter.**
- Bu envanter kapsam temeli. **26 yeni araç** (§5 kataloğu) bağlayıcı sayı.
- Charter "15" figürünü 26'ya günceller (§4 gerekçesi).
- 3 dalga (§6) charter'da adıma bölünür (7.2-7.13).
- §7'deki yeni altyapılar (vergi dilim, ceza takvim, miras/tenkis) her biri ön
  adım olarak planlanır.
- Yeni mimari karar az (altyapı oturmuş); charter kompakt tutulabilir
  (~200-250 satır, Faz 6 charter pattern).
- #42 (rapor merge süreci) + enum comment drift charter'da deftere alınır.

> Bu dalga/sayı önerileri envanter seviyesindedir; **kesin kilit Adım 7.1
> charter'ın işidir.** Yeni kod yok, test eklenmedi (doc-only).

# Lex Calculus — Faz 7 Yol Haritası

## Hesaplama Tamamlama

**Başlangıç:** 5 Haziran 2026 · **Bitiş:** 17 Haziran 2026 · **Süre:** ~13 gün
(charter 3-4 hafta tahmini) · **Charter:** [phase-7-charter.md](./phase-7-charter.md)
· **Envanter:** [phase-7-scope-inventory.md](./phase-7-scope-inventory.md)

🏁 **Faz 7 — Hesaplama Tamamlama ✅ TAMAMLANDI** — 3 dalga, 12 alt adım (7.2-7.13)
+ 1 hotfix (7.13a), 26 yeni hesaplama aracı. **17 → 43 aktif araç** (9 kategori
tam %100 kapsama). Tag: `phase-7-complete` (smoke onayı sonrası annotated tag,
mesaj: `temp/phase-7-complete-tag-message.txt`).

---

## Dalga özeti

| Dalga | Konu | Adım Aralığı | Tahmin | Durum |
|---|---|---|---|---|
| A | Gayrimenkul + Aile/Miras | 7.2-7.6 | 5-6 gün | ✅ |
| B | Ceza + Vergi/İdare | 7.7-7.10 | 5-6 gün | ✅ |
| C | Ticaret + Bilirkişi + closeout | 7.11-7.13 | 3-4 gün | ✅ |

## Adım tablosu

| Adım | Başlık | Araçlar | Yeni Altyapı | Karmaşıklık | Durum |
|---|---|---|---|---|---|
| 7.0 | Envanter | - | - | - | ✅ |
| 7.1 | Charter | - | - | - | ✅ (bu commit) |
| 7.2 | D Gayrimenkul altyapı + D1 Arsa Payı | 1 | D base | BASİT | ✅ |
| 7.3 | D2 + D3 | 2 | - | KARMAŞIK + ORTA | ✅ |
| 7.4 | D4 + D5 | 2 | - | ORTA + ORTA | ✅ |
| 7.5 | E altyapı + E1 + E4 | 2 | E base | ORTA + ORTA | ✅ |
| 7.6 | Miras servisi + E2 + E3 + Dalga A closeout | 2 | Miras servisi | KARMAŞIK × 2 | ✅ |
| 7.7 | Ceza takvim + F1 + F2 | 2 | Ceza servisi | ORTA + KARMAŞIK | ✅ |
| 7.8 | F3 + F4 + F5 | 3 | - | KARMAŞIK + ORTA + BASİT | ✅ |
| 7.9 | Vergi dilim + G1 + G2 | 2 | Vergi entity | KARMAŞIK + BASİT | ✅ |
| 7.10 | G3 + G4 + G5 + Dalga B closeout | 3 | - | ORTA × 2 + KARMAŞIK | ✅ |
| 7.11 | H1 + H2 + H3 | 3 | - | ORTA × 3 | ✅ |
| 7.12 | I1 + I2 + I3 + I4 | 4 | - | BASİT × 2 + KARMAŞIK + ORTA | ✅ |
| 7.13a | F1 Ceza Erteleme route hotfix | - | - | - | ✅ |
| 7.13 | Faz 7 closeout | - | - | - | ✅ |

---

## Dalga A — Gayrimenkul + Aile/Miras (7.2-7.6, 9 araç) 🏁 ✅

### Adım 7.2 — D Gayrimenkul altyapı + D1 Arsa Payı ✅
- **Kapsam:** D kategori ilk aracı + D1 Arsa Payı Hesabı.
- **Mevzuat:** 634 s.K. m.3 (Kat Mülkiyeti — arsa payı).
- **Yöntem:** Değer ağırlıklı yüzölçümü (yüzölçümü × kullanım türü × kat etkisi),
  1000 üzerinden dağıtım. Katsayılar FormulaParameter (`arsa-payi/katsayi.*`, 6 satır).
- **Sapma:** Parametre "HAZIR (in-code)" öngörülmüştü; Faz 2 pattern'e uymak için
  katsayılar in-code yerine zaman-versiyonlu FormulaParameter olarak seed edildi
  (admin ileride ayarlayabilir, tutarlı lookup). DTO/Calculator/View/Controller/test
  Faz 2 pattern birebir (spec'in ViewModel + throw-validation pseudo-kodu yerine
  gerçek `@model Input` + `ViewData["Result"]` + `ValidationErrors` sözleşmesi).
- **#43 enum comment drift** (D-I "Faz 5" → Faz 7) düzeltildi.
- **Test:** +6 (838 → 844). Sitemap/kategori landing otomatik (Active register).

### Adım 7.3 — D2 Kamulaştırma + D3 Ecrimisil ✅
- **D2 Kamulaştırma Bedeli** (KARMAŞIK) — 2942 s.K. m.11. İki yöntem: emsal
  karşılaştırma (objektif artış %100 cap, Yargıtay 5. HD K. 2005/675) +
  gelir kapitalizasyonu (net gelir / kapitalizasyon oranı — perpetüite).
  Opsiyonel yapı (bina) bedeli. Referans karar test (Karar 4) ✅.
- **D3 Ecrimisil** (ORTA) — TMK m.995 + Yargıtay 1. HD (K. 2014/4059). İlk dönem
  rayiç kira + yıllık ÜFE birikimli artış. Yerleşik içtihat prensip testi ✅.
- **Sapma (reuse):** D2 için `IActuarialService.AnnuityPresentValue` öngörülmüştü;
  ancak o **sonlu** annuity (maluliyet gibi ömür-bağlı gelir kaybı) içindir —
  kamulaştırma gelir kapitalizasyonu **perpetüel** arazi gelirini değerler
  (`gelir / oran`). Doğru model basit kapitalizasyon; AnnuityPresentValue
  kullanılmadı (durma notu escape-hatch, raporlandı).
- **ÜFE parametresi** global `*`/`ufe.yillik` (TÜFE değil — Yargıtay ÜFE kullanır).
  Eksik-yıl tam-eşleşme tespiti + uyarı. tech-debt #44.
- **Test:** +14 (844 → 858).

### Adım 7.4 — D4 Kat Karşılığı + D5 Hâsılat Kira ✅
- **D4 Kat Karşılığı İnşaat Paylaşımı** (ORTA) — TBK + içtihat. İki yöntem:
  oransal (arsa değeri / (arsa + inşaat)) + sabit sözleşme oranı. Çıktı arsa
  sahibi/müteahhit pay (TL) + yaklaşık bağımsız bölüm sayısı (referans).
- **D5 Hâsılat Kira Hesabı** (ORTA) — TBK + ticari kira (AVM modeli). Ciro × oran,
  min güvence (taban) + max tavan clamp; uygulanan kural raporlanır
  (CiroBazli / MinimumGuvence / MaksimumTavan).
- Her iki araç **parametresiz** (saf hesap, DB gerekmez) — testler hızlı.
- View uyarısı her ikisinde: "Bu sonuç sözleşme(/detaylarının) yerine geçmez."
- **Test:** +12 (858 → 870).

> 🏁 **Dalga A — Gayrimenkul (D1-D5) tamamlandı.** 5 araç aktif: Arsa Payı,
> Kamulaştırma Bedeli, Ecrimisil, Kat Karşılığı, Hâsılat Kira. Kalan Dalga A:
> E Aile/Miras (7.5-7.6).

### Adım 7.5 — E Aile/Miras altyapı + E1 Nafaka + E4 Mal Rejimi ✅
- E kategori view klasörü açıldı: `/Views/Hesapla/AileMiras/` (registry-driven
  landing/index/sitemap — ek kod gerekmedi).
- **E1 Nafaka** (ORTA) — TMK m.169/175/182. Tek calculator, 3 nafaka türü
  (iştirak / yoksulluk / tedbir) + 2 hesap türü (yeni belirleme / artış)
  dispatch. İştirak: 12 katsayı (baz oran + yaş + eğitim + şehir), asgari ücret
  %25 alt sınır. Yoksulluk: gelir farkı × %30 × evlilik süresi katsayısı (YHGK
  2024-15.10). Artış: **ITUFEService reuse** — TÜFE 12 aylık ortalama
  (`tufe-12-ay-ort` slug, KiraArtisi ile aynı kaynak; yeni seed gerekmedi).
  17 katsayı `nafaka` slug'ı altında seed (bkz. tech-debt madde 45 — heuristik
  referans, hukuk incelemesi gerek).
- **E4 Mal Rejimi Tasfiyesi** (ORTA) — TMK m.218-241, **parametresiz**.
  Artık değer = edinilmiş − borç (negatif → 0); katılma alacağı = karşı eş
  artık değer × ½; kişisel mal (evlilik öncesi + miras/bağış) tasfiye dışı.
- Her iki araçta güçlü `info-box--warning`: "Bu sonuç mahkeme kararı / bilirkişi
  raporu yerine geçmez, referans niteliğindedir."
- Calculator sayısı: 22 → 24 (Dalga A: 7/9 araç). **Test:** +13 (870 → 883).

### Adım 7.6 — Miras dağıtım servisi + E2 + E3 + Dalga A closeout ✅
- **Altyapı:** `IInheritanceDistributionService` (Karar 3) — `Core/Services/`
  (saf hesap, interface + impl Core'da). TMK m.495-501 zümre hiyerarşisi
  (1./2./3. derece), m.498 halefiyet (ölmüş çocuk → torun, ölmüş kardeş →
  yeğen), m.506 saklı pay oranları. 4. derece (büyük ana-baba altsoyu) kapsam
  dışı → tech-debt #46.
- **E2 Miras Payı** (KARMAŞIK, TMK m.495-501) — servisi sarar; eş + 3 derece +
  halefiyet kombinasyonları. 8 test (sentetik referans dahil).
- **E3 Tenkis** (KARMAŞIK, TMK m.506 + m.560-571) — net miras = malvarlığı +
  bağışlar (m.565); saklı pay ihlali; tenkis sırası önce vasiyet sonra son
  bağıştan geriye (m.561). 7 test (sentetik referans dahil).
- **Karar/sapma:** (1) Servis DTO/identifier'ları ASCII (`MirasciPay`,
  `MirasciTuru` — kod konvansiyonu; roadmap'teki `ı` yalnız notasyondu).
  (2) `SakliPayOrani` imzasına `int aktifDerece = 0` eklendi (eş 1./2. zümre ile
  tamamı, diğer ¾ — TMK m.506/3 doğruluğu; tek-arg çağrı uyumlu kalır).
- Calculator: 24 → 26. **Test:** +17 (883 → 900).

> 🏁 **Dalga A tamamlandı (Adım 7.2-7.6, 9 araç).** D Gayrimenkul (5): Arsa
> Payı, Kamulaştırma Bedeli, Ecrimisil, Kat Karşılığı, Hâsılat Kira. E
> Aile/Miras (4): Nafaka, Mal Rejimi Tasfiyesi, Miras Payı, Tenkis. Aktif
> araç 17 → 26. Sonraki: **Dalga B** — Ceza + Vergi/İdare (Adım 7.7-7.10).

---

## Dalga B — Ceza + Vergi/İdare (7.7-7.10, 10 araç) 🏁 ✅

### Adım 7.7 — Ceza takvim servisi + F1 + F2 ✅
- **Altyapı:** `ICriminalCalendarService` (Karar 2) — gün/ay/yıl, tahliye
  tarihi, resmi tatil seed 2020-2030 (Diyanet referansı).
- **F1 Ceza Erteleme Süresi** (ORTA) — TCK m.51, yetişkin 2 yıl / çocuk 3 yıl.
- **F2 Koşullu Salıverilme Tarihi** (KARMAŞIK) — 5275 s.K. m.107, suç tipi
  enum (Genel 2/3, Terör/Cinsel/Örgütlü/Ağır 3/4) + tutukluluk mahsubu.
- Test: +17 (900 → 917). Calculator 26 → 28.

### Adım 7.8 — F3 + F4 + F5 ✅
- **F3 Dava Zamanaşımı** (KARMAŞIK) — TCK m.66-67, 5 suç ağırlığı kategorisi
  (8/15/20/25/30 yıl) + kesinti + m.67/4 mutlak sınır (asli × 1.5).
- **F4 Adli Para Cezası** (ORTA) — TCK m.52, Direkt + Hapis Çevrim.
- **F5 Tutukluluk Mahsup** (BASİT) — TCK m.63 + 5275 s.K. m.108, inclusive
  gün hesabı + opsiyonel adli para mahsubu.
- Test: +18 (917 → 935). Calculator 28 → 31. F kategori 5/5 tamamlandı.

### Adım 7.9 — Vergi dilim altyapı + G1 + G2 ✅
- **Altyapı:** `TaxBracket` entity (Karar 1) — `TaxBrackets` tablosu,
  `ITaxBracketService` (24sa cache + marjinal dilim hesabı), 10 seed satırı.
- **G1 Veraset ve İntikal Vergisi** (KARMAŞIK) — 7338 s.K. m.4 + m.16, 2026
  tarifesi (RG 31.12.2025/33124 5.Mük., 57 Seri No'lu Tebliğ). Veraset
  %1-10, ivazsız %10-30; istisna 2.907.136 / 5.817.845 / 66.935 TL.
- **G2 Tapu Harcı** (BASİT) — 492 s.K., alıcı %2 + satıcı %2 (toplam %4).
- Test: +16 (935 → 951). Calculator 31 → 33.

### Adım 7.10 — G3 + G4 + G5 + Dalga B closeout ✅
- **G3 Damga Vergisi** (ORTA) — 488 s.K., 5 belge türü oranı (‰1,89-9,48)
  + 2026 azami sınır 5.281.302,40 TL cap. FormulaParameter (6 satır).
- **G4 KDV İadesi** (ORTA) — 3065 s.K. m.32, parametresiz formül +
  mahsup öncesi/sonrası raporlama.
- **G5 Vergi Cezası ve Gecikme Faizi** (KARMAŞIK) — 213 s.K. m.341-376,
  vergi ziyaı %50 / kaçakçılık %100 / usulsüzlük maktu + m.112 basit aylık
  faiz (yıl segmentasyonlu). FormulaParameter zaman-versiyonlu aylık oran
  (2020-2026, 7 satır + 1 zam).
- F3 cleanup: `DavaZamanasimiCalculator` `ICriminalCalendarService` inject
  kaldırıldı (tech-debt #49 ÇÖZÜLDÜ).
- Test: +16 (951 → 967). Calculator 33 → 36. G kategori 5/5 tamamlandı.

> 🏁 **Dalga B tamamlandı (Adım 7.7-7.10, 10 araç).** F Ceza (5): Erteleme,
> Koşullu Salıverilme, Zamanaşımı, Adli Para, Tutukluluk Mahsup. G Vergi/İdare
> (5): Veraset, Tapu Harcı, Damga, KDV İade, Vergi Cezası. Aktif araç 26 → 36.
> Sonraki: **Dalga C** — Ticaret + Bilirkişi + Faz 7 closeout (Adım 7.11-7.13).

---

## Dalga C — Ticaret + Bilirkişi + closeout (7.11-7.13, 7 araç) 🏁 ✅

### Adım 7.11 — H1 + H2 + H3 ✅
- **H1 Şirket Tasfiye Payı** (ORTA) — 6102 s.K. TTK m.543 + m.642, girdi-bazlı
  bilanço (varlıklar − borçlar − tasfiye gideri = tasfiye edilebilir net),
  pay sahibi başına dağıtım.
- **H2 Anonim Şirket Kâr Payı** (ORTA) — 6102 s.K. TTK m.508 + m.519, 1.
  temettü (yedek akçe sonrası dağıtılabilir kârın %5'i pay sahiplerine garanti)
  + 2. temettü (yönetim kurulu kararına bağlı serbest).
- **H3 Sözleşme Cezası** (ORTA) — TBK m.179-182, ceza şartı + TBK m.182/3
  hâkim indirimi (fahiş ceza ölçütü).
- Tüm 3 araç **parametresiz** (saf hesap, DB gerekmez).
- Test: +18 (967 → 985). Calculator 36 → 39.

### Adım 7.12 — I1 + I2 + I3 + I4 ✅
- **I1 PMF Yaşam Tablosu Sorgulama** (BASİT) — TRH 2010, `ILifeTableService`
  reuse (Faz 1 altyapısı). Yaş + cinsiyet → beklenen yaşam süresi + ölüm olasılığı.
- **I2 İskontolu Nakit Akışı** (BASİT) — finans matematiği, `IActuarialService`
  annuity/discount reuse. Nakit akışı + iskonto oranı + dönem → bugünkü değer.
- **I3 Hakkaniyetli Tazminat Simülatörü** (KARMAŞIK) — TBK m.51, 9 katsayı
  (ekonomik durum × 3, olay ağırlığı × 3, yaş × 3) FormulaParameter
  (`hakkaniyetli-tazminat/*`, heuristik referans — tech-debt #51). Referans
  karar test (Karar 4) ✅. View'da güçlü uyarı.
- **I4 Çevresel Zarar Tazminatı** (ORTA) — 2872 s.K. m.28, kirlilik türü +
  alan büyüklüğü + süre + kalkınma maliyeti. FormulaParameter (kirlilik türü
  katsayısı, ~5 satır).
- Test: +15 (985 → 1000). Calculator 39 → 43. **🎯 43/43 milestone.**

> 🏁 **Dalga C tamamlandı (Adım 7.11-7.12, 7 araç).** H Ticaret (3): Tasfiye Payı,
> Kâr Payı, Sözleşme Cezası. I Bilirkişi (4): PMF Sorgu, DCF, Hakkaniyetli Tazminat,
> Çevresel Zarar. Aktif araç 36 → 43 (charter hedef %100). Sonraki: Adım 7.13a
> (F1 hotfix) + Adım 7.13 (Faz 7 closeout).

### Adım 7.13a — F1 Ceza Erteleme view sapması hotfix ✅
- **Bağlam:** Closeout öncesi smoke test taramasında browser'da `/hesapla/ceza/ceza-erteleme`
  "yakında" placeholder gösteriyordu — Calculator + DI register + test + view dosyası
  hepsi DOĞRU; sapma `HesaplaController` route attribute'undaydı.
- **Kök sebep:** Adım 7.7'de F1 GET/POST route `[HttpGet("ceza/erteleme")]`
  yazılmış; ancak registry slug `"ceza-erteleme"`. Diğer 4 Ceza tool'u (F2-F5)
  `ceza/{full-slug}` formatına uyuyor; F1 tek anomali. Kullanıcı slug-tabanlı
  URL'ye gidince spesifik route match etmedi → generic catch-all
  `[HttpGet("{categorySlug}/{toolSlug}")]` "yakında" placeholder döndürdü.
- **Düzeltme:** `HesaplaController.cs` 2 satır:
  `[HttpGet("ceza/erteleme")]` → `[HttpGet("ceza/ceza-erteleme")]`,
  POST aynı. Form `asp-action` tag helper kullandığı için view dokunulmadı.
- **Test:** 1000 yeşil (regresyon 0, test eklenmedi). Build 0/0.
- **Sapma kaydı:** tech-debt #52 (manuel smoke test her dalga sonu zorunlu —
  otomatik test view render uyumunu kapsamıyor, F1 sapması Faz 7 boyunca
  yakalanmadı, closeout'a kadar geldi).
- **Commit:** `55cdafb`.

### Adım 7.13 — Faz 7 closeout ✅
- Roadmap final ✅ + 🏁 banner + final istatistikler.
- README durum bloğu Faz 7 ✅ TAMAMLANDI banner.
- tech-debt güncel: #52 yeni eklendi, çözülen (#43, #49) + açık ayrımı net.
- temp/phase-7-complete-tag-message.txt hazır (ASCII safe, multi-line).
- `phase-7-complete` annotated tag smoke onayı sonrası ayrı komut ile.

---

## Karmaşıklık dağılımı (envanter §5)

| Karmaşıklık | Adet |
|---|---|
| BASİT | 5 |
| ORTA | 12 |
| KARMAŞIK | 9 |
| **Toplam** | **26** |

> Not: Kanonik dağılım envanter §5'tir (BASİT 5 / ORTA 12 / KARMAŞIK 9 = 26,
> "tahmini" etiketli). Yukarıdaki adım tablosundaki tek tek araç karmaşıklık
> etiketleri çalışma tahminidir (envanterin per-slug etiketleri 6/13/7'ye
> toplanıyordu — §5 özet sayımı otorite, sınır araçlar implementasyonda
> kesinleşir). KARMAŞIK 9 araç tamamı için referans karar test zorunlu (Karar 4).

**Toplam: 26 araç** — Faz 7 sonu 17 + 26 = **43 aktif hesaplama aracı**.

---

## 🏁 Faz 7 final istatistikler

| Metrik | Faz başı | Faz sonu | Delta |
|---|---|---|---|
| Aktif hesaplama aracı | 17 | **43** | +26 (%100 katalog kapsama) |
| Test | 838 | **1000** | +162 (regresyon 0) |
| Build | 0/0 | 0/0 | warnaserror korundu |
| Yeni altyapı servisi | — | **3** | IInheritanceDistributionService + ICriminalCalendarService + TaxBracket entity |
| Yeni view klasörü | — | **6** | Gayrimenkul, AileMiras, Ceza, VergiIdare, Ticaret, Bilirkisi |
| Yeni migration | — | **1** | AddTaxBrackets |
| FormulaParameter seed | — | **~75 satır** | yeni |
| TaxBracket seed | — | **10 satır** | yeni |
| Commit | — | **13** | 11 implementation + 1 hotfix (7.13a) + 1 closeout |
| Charter karar | 4/4 | 4/4 ✓ | TaxBracket entity, CriminalCalendarService, InheritanceDistributionService, sentetik referans test pattern |

### Dalga kırılımı (gerçek)

| Dalga | Adım | Araç | Commit | Test delta |
|---|---|---|---|---|
| A — Gayrimenkul + Aile/Miras | 7.2-7.6 | 9 | 5 | 838 → 900 (+62) |
| B — Ceza + Vergi/İdare | 7.7-7.10 | 10 | 4 | 900 → 967 (+67) |
| C — Ticaret + Bilirkişi | 7.11-7.12 | 7 | 2 | 967 → 1000 (+33) |
| Hotfix | 7.13a | F1 route | 1 | 1000 (regresyon 0) |
| Closeout | 7.13 | docs | 1 | — |

### Kapanan tech-debt (Faz 7'de)
- **#43** Enum comment drift (CalculatorCategory) — Adım 7.2
- **#49** F3 DavaZamanasimi unused ICriminalCalendarService inject — Adım 7.10

### Açık tech-debt (Faz 8'e devredildi)
- **Hukuk profesyoneli incelemesi:** #45 nafaka katsayı, #46 TMK m.501 4. derece,
  #47 yarım kardeş ayrımı, #51 hakkaniyetli tazminat çarpan kalibrasyon.
- **Otomatik veri çekme:** #44 ÜFE elle seed, #48 dini bayram kameri takvim,
  #50 TaxBracket admin UI + otomatik dilim.
- **Süreç:** #42 referans belge dangling commit, **#52 manuel smoke test
  her dalga sonu zorunlu** (yeni — F1 sapması Adım 7.13 closeout'a kadar
  yakalanmadı).

### Süreç öğrenmeleri (Faz 7'den Faz 8'e)
1. **Pseudo-kod ≠ ground truth** — Claude Code her komutta varsayımları gerçek
   koda göre düzeltti (E4 net alacak yönü, ILifeTableService imzası, TÜFE
   altyapı reuse, vb.). Pattern Faz 8'de devam.
2. **F1 route sapması (7.7) Faz 7 boyunca yakalanmadı** — generic catch-all
   placeholder maskeleme yaptı. Otomatik test view render doğrulamasını
   kapsamıyor. #52 olarak deftere alındı.
3. **#41 envanter denetim usage taraması her komutta uygulandı** — Faz 6
   öğrenmesi pattern olarak yerleşti.
4. **Sentetik referans test pattern (Karar 4)** — Yargıtay metni gerektirmeden
   mevzuat formül doğrulama için yeterli.
5. **Parametresiz Calculator + DB'siz test pattern** (Adım 7.4'ten itibaren) —
   hızlı test koşumu, izolasyon temiz.
6. **Yeni altyapı Core vs Infrastructure ayrımı:** DB IO yapanlar Infrastructure
   (TaxBracketService), saf hesap Core (Inheritance, CriminalCalendar).

### Manuel smoke test (kullanıcı doğrulaması)
6 araç: D1 Arsa Payı, E2 Miras Payı, F2 Koşullu Salıverilme, G1 Veraset,
I3 Hakkaniyetli Tazminat, F1 Ceza Erteleme (hotfix sonrası).

### Sonraki: Faz 8 — Lansman Planı
- Production deploy
- Hukuk profesyoneli incelemesi (9 KARMAŞIK araç + #45 + #46 + #47 + #51)
- İlk kullanıcılar + geri bildirim
- Faz 8 teması veri-temelli karar.

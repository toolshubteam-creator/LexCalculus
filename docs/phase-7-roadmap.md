# Lex Calculus — Faz 7 Yol Haritası

## Hesaplama Tamamlama

**Başlangıç:** 5 Haziran 2026 · **Charter:** [phase-7-charter.md](./phase-7-charter.md)
· **Envanter:** [phase-7-scope-inventory.md](./phase-7-scope-inventory.md)

⏳ **Faz 7 başladı** — 3 dalga, 12 alt adım (7.2-7.13), 26 yeni hesaplama aracı.
Hedef: 17 → 43 aktif araç (9 kategori tam). Tag (kapanış): `phase-7-complete`.

---

## Dalga özeti

| Dalga | Konu | Adım Aralığı | Tahmin | Durum |
|---|---|---|---|---|
| A | Gayrimenkul + Aile/Miras | 7.2-7.6 | 5-6 gün | ⏳ |
| B | Ceza + Vergi/İdare | 7.7-7.10 | 5-6 gün | ⏳ |
| C | Ticaret + Bilirkişi + closeout | 7.11-7.13 | 3-4 gün | ⏳ |

## Adım tablosu

| Adım | Başlık | Araçlar | Yeni Altyapı | Karmaşıklık | Durum |
|---|---|---|---|---|---|
| 7.0 | Envanter | - | - | - | ✅ |
| 7.1 | Charter | - | - | - | ✅ (bu commit) |
| 7.2 | D Gayrimenkul altyapı + D1 Arsa Payı | 1 | D base | BASİT | ✅ |
| 7.3 | D2 + D3 | 2 | - | KARMAŞIK + ORTA | ⏳ |
| 7.4 | D4 + D5 | 2 | - | ORTA + ORTA | ⏳ |
| 7.5 | E altyapı + E1 + E4 | 2 | E base | ORTA + ORTA | ⏳ |
| 7.6 | Miras servisi + E2 + E3 + Dalga A closeout | 2 | Miras servisi | KARMAŞIK × 2 | ⏳ |
| 7.7 | Ceza takvim + F1 + F2 | 2 | Ceza servisi | ORTA + KARMAŞIK | ⏳ |
| 7.8 | F3 + F4 + F5 | 3 | - | KARMAŞIK + ORTA + BASİT | ⏳ |
| 7.9 | Vergi dilim + G1 + G2 | 2 | Vergi entity | KARMAŞIK + BASİT | ⏳ |
| 7.10 | G3 + G4 + G5 + Dalga B closeout | 3 | - | ORTA × 2 + KARMAŞIK | ⏳ |
| 7.11 | H1 + H2 + H3 | 3 | - | ORTA × 3 | ⏳ |
| 7.12 | I1 + I2 + I3 + I4 | 4 | - | BASİT × 2 + KARMAŞIK + ORTA | ⏳ |
| 7.13 | Faz 7 closeout | - | - | - | ⏳ |

---

## Dalga A — Gayrimenkul + Aile/Miras (7.2-7.6, 9 araç)

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

### Adım 7.3 — D2 Kamulaştırma + D3 Ecrimisil ⏳
- **D2 Kamulaştırma Bedeli** (KARMAŞIK) — 2942 s.K. YENİ-PARAMETRE
  (kapitalizasyon/kira çarpanı) + IActuarialService değerleme reuse.
- **D3 Ecrimisil** (ORTA) — TMK/HMK, mevcut faiz altyapısı reuse.

### Adım 7.4 — D4 Kat Karşılığı + D5 Hâsılat Kira ⏳
- **D4 Kat Karşılığı İnşaat Paylaşımı** (ORTA) — TBK genel, girdi-bazlı.
- **D5 Hâsılat Kira Hesabı** (ORTA) — TBK ticari kira, girdi-bazlı.

### Adım 7.5 — E Aile/Miras altyapı + E1 Nafaka + E4 Mal Rejimi ⏳
- E kategori view/landing iskeleti.
- **E1 Nafaka** (ORTA) — TMK m.182/197/364, ITUFEService reuse (TÜFE artış).
- **E4 Mal Rejimi Tasfiyesi** (ORTA) — TMK m.218 vd., edinilmiş/kişisel ayrım.

### Adım 7.6 — Miras dağıtım servisi + E2 + E3 + Dalga A closeout ⏳
- **Altyapı:** `IInheritanceDistributionService` (Karar 3) — TMK m.495-501
  derece hiyerarşisi + saklı pay.
- **E2 Miras Payı** (KARMAŞIK) — TMK m.495-501. Referans karar test zorunlu.
- **E3 Tenkis** (KARMAŞIK) — TMK m.560, E2 saklı pay altyapısına bağımlı.
  Referans karar test zorunlu.
- Dalga A closeout (mini doküman güncelleme).

---

## Dalga B — Ceza + Vergi/İdare (7.7-7.10, 10 araç)

### Adım 7.7 — Ceza takvim servisi + F1 + F2 ⏳
- **Altyapı:** `ICriminalCalendarService` (Karar 2) — gün/ay/yıl, tahliye
  tarihi, resmi tatil seed.
- **F1 Ceza Erteleme Süresi** (ORTA) — 5237 s.K. m.51.
- **F2 Koşullu Salıverilme Tarihi** (KARMAŞIK) — 5275 s.K. m.107. Suç tipine
  göre 2/3 veya 3/4 oran + uyarı metni. Referans karar test zorunlu.

### Adım 7.8 — F3 + F4 + F5 ⏳
- **F3 Dava Zamanaşımı** (KARMAŞIK) — 5237 s.K. m.66-67, YENİ-PARAMETRE
  (suç tipi süre tablosu) + takvim. Referans karar test zorunlu.
- **F4 Adli Para Cezası** (ORTA) — 5237 s.K. m.52, gün × günlük tutar.
- **F5 Tutukluluk Mahsup** (BASİT) — CMK / 5275 s.K., takvim/süre mahsup.

### Adım 7.9 — Vergi dilim altyapı + G1 + G2 ⏳
- **Altyapı:** `TaxBracket` entity (Karar 1) — artan oranlı matrah dilimleri,
  tarih bazlı versiyonlama.
- **G1 Veraset ve İntikal Vergisi** (KARMAŞIK) — 7338 s.K., dilim + istisna.
  Referans karar test zorunlu. Dilim tutarları elle seed (Resmi Gazete).
- **G2 Tapu Harcı** (BASİT) — 492 s.K., YENİ-PARAMETRE (harç oranı).

### Adım 7.10 — G3 + G4 + G5 + Dalga B closeout ⏳
- **G3 Damga Vergisi** (ORTA) — 488 s.K., YENİ-PARAMETRE (sözleşme türü
  oranı + azami sınır).
- **G4 KDV İadesi Alacağı** (ORTA) — 3065 s.K. m.29, indirimli oran.
- **G5 Vergi Cezası ve Gecikme Faizi** (KARMAŞIK) — 213 s.K., gecikme zammı
  + TaxBracket reuse. Referans karar test zorunlu.
- Dalga B closeout (mini).

---

## Dalga C — Ticaret + Bilirkişi + closeout (7.11-7.13, 7 araç)

### Adım 7.11 — H1 + H2 + H3 ⏳
- **H1 Şirket Tasfiye Payı** (ORTA) — 6102 s.K. TTK, girdi-bazlı bilanço.
- **H2 Anonim Şirket Kâr Payı** (ORTA) — 6102 s.K. TTK, 1./2. temettü kuralları.
- **H3 Tazminat Sözleşme Cezası** (ORTA) — TBK m.179-182, ceza şartı + hâkim indirimi.

### Adım 7.12 — I1 + I2 + I3 + I4 ⏳
- **I1 PMF Yaşam Tablosu Sorgulama** (BASİT) — TRH 2010, ILifeTableService reuse.
- **I2 İskontolu Nakit Akışı** (BASİT) — finans matematiği, IActuarialService
  annuity/discount reuse.
- **I3 Hakkaniyetli Tazminat Simülatörü** (KARMAŞIK) — emsal kararlar, girdi-bazlı
  tahmin. Referans karar test zorunlu.
- **I4 Çevresel Zarar Tazminatı** (ORTA) — 2872 s.K., YENİ-PARAMETRE (kirlilik
  türü katsayısı).

### Adım 7.13 — Faz 7 closeout ⏳
- README + roadmap + tech-debt güncelleme.
- Tamamlanma kriterleri doğrulama (§8 charter).
- `phase-7-complete` annotated tag.

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

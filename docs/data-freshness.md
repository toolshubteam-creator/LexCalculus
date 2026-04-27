# Lex Calculus — Veri Tazelik Takip Listesi

> **Amaç:** Hesaplayıcılarda kullanılan dış kaynaklı verilerin (TÜFE, TCMB, RG yayınları) ne zaman güncellenmesi gerektiğini takip etmek.
>
> **Faz 3 hedefi:** Bu kontrolün admin panelinde otomatik widget olarak gösterilmesi (`docs/phase-3-roadmap.md` — Adım 3.3).
>
> **Şu an (Faz 2 sonu — Adım 2.23):** Manuel takip için bu dosyaya bakılır.

---

## Faz 2 Sonu Durum Özeti

Adım 2.23 itibariyle:
- **17 calculator aktif** (İş Hukuku 7, Aktüerya 5, Faiz 5)
- **~95 FormulaParameter satırı** (yasal-faiz 2, tcmb-avans 18, tufe-12-ay-ort 64, kidem-tazminati 5, ihbar/asgari/damga vb.)
- **200+ LifeTableRow** (TRH 2010, yaş 0-99 × 2 cinsiyet)
- **13 DB tablosu** (CalculationHistory dahil — Adım 2.22)
- **219 unit test** geçiyor

---

## AYLIK GÜNCELLEME

### TÜFE 12 Aylık Ortalama

| Alan | Değer |
|---|---|
| **ToolSlug** | `tufe-12-ay-ort` |
| **Hesaplayıcı** | Kira Artış Tespiti (`/hesapla/faiz/kira-artisi`) |
| **Kaynak** | TÜİK — https://data.tuik.gov.tr (her ayın 3'ünde, saat 10:00) |
| **Yedek** | Legalbank, ASMMMO, Alomaliye, hesaplama.net |
| **Format** | Key=`YYYY-MM`, Value=yüzde (örn. `2026-03` → `32.82`) |
| **Seed satır sayısı** | 64 (2020-12 → 2026-03) |
| **Son güncelleme** | 2026-03 (Adım 2.20'de seed edildi) |
| **Sıradaki kontrol** | Her ayın 3'ü, ekleme: yeni `YYYY-MM` satırı |
| **Sorumlu** | manual — admin@lexcalculus.local (Faz 3'te admin paneli) |
| **Hatalı veri etkisi** | Kira Artışı yanlış üst sınır verir; TBK m.344/1 emredici, mahkemede hesap geçersiz olur |

---

## YARI YIL / DÖNEMSEL GÜNCELLEME

### TCMB Avans Oranı

| Alan | Değer |
|---|---|
| **ToolSlug** | `tcmb-avans` |
| **Hesaplayıcı** | Ticari Temerrüt Faizi (`/hesapla/faiz/ticari-temerrut-faizi`), Akdî Temerrüt Faizi alt sınır |
| **Kaynak** | TCMB Reeskont ve Avans Faiz Oranları — https://www.tcmb.gov.tr |
| **Güncelleme** | Genelde 31 Aralık ve 30 Haziran civarı + arada TCMB karar değişimleri (3095 m.2'ye göre yok sayılır) |
| **3095 m.2 algoritması** | İlk 6 ay = önceki yıl 31 Aralık oranı; ikinci 6 ay = 30 Haziran oranı (≥5pp farklıysa, aksi halde devam) |
| **Seed satır sayısı** | 18 (2018-06-29 → 2025-12-20) |
| **Sıradaki kontrol** | Ocak ilk haftası, Temmuz ilk haftası |
| **Sorumlu** | manual — admin@lexcalculus.local (Faz 3'te admin paneli) |
| **Hatalı veri etkisi** | Ticari faiz hesabı yanlış; alacaklı düşük tahsilat alır veya yüksek dava açıp reddedilir |

### Kıdem Tazminatı Tavanı

| Alan | Değer |
|---|---|
| **ToolSlug** | `kidem-tazminati` / Key=`tavan` |
| **Hesaplayıcı** | Kıdem Tazminatı (`/hesapla/is-hukuku/kidem-tazminati`) |
| **Kaynak** | Hazine ve Maliye Bakanlığı yıllık 2 adet Mali ve Sosyal Haklar Genelgesi — https://ms.hmb.gov.tr |
| **Güncelleme** | Ocak ve Temmuz başı (yarı yıl güncelleme) |
| **Seed satır sayısı** | 5 (2024-01, 2024-07, 2025-01, 2025-07, 2026-01) |
| **Adım 2.23'te eklenen** | 2024-01-01 (35.058,58 TL), 2024-07-01 (41.828,42 TL) — daha önce manuel SQL ile DB'ye girilmişti, seed'de yoktu |
| **Sıradaki kontrol** | Temmuz 2026 başı (yeni 2026/2 genelgesi) |
| **Sorumlu** | manual — admin@lexcalculus.local (Faz 3'te admin paneli) |
| **Hatalı veri etkisi** | Kıdem tazminatı tavanı eksik/fazla; işçi yüksek alır veya işveren eksik öder, dava açılır |

### Asgari Ücret

| Alan | Değer |
|---|---|
| **ToolSlug** | `*` / Key=`asgari-ucret-brut` (global parametre) |
| **Hesaplayıcı** | Asgari Ücret Uyumluluk Kontrolü, İş Hukuku alt sınır kontrolleri |
| **Kaynak** | Asgari Ücret Tespit Komisyonu, Resmi Gazete |
| **Güncelleme** | Aralık-Ocak (yıllık), bazen Haziran-Temmuz (ara zam — son 2022, 2023) |
| **Seed satır sayısı** | 3 (2024, 2025, 2026) |
| **Sıradaki kontrol** | Aralık 2026 - Ocak 2027 (2027 yıllık zam) |
| **Sorumlu** | manual — admin@lexcalculus.local (Faz 3'te admin paneli) |
| **Hatalı veri etkisi** | Asgari Ücret Uyumluluk yanlış sonuç döner; eksik/fazla alacak hesabı yapılır |

---

## OLAY BAZLI GÜNCELLEME

### Yasal Faiz Oranı (3095 m.1)

| Alan | Değer |
|---|---|
| **ToolSlug** | `yasal-faiz` / Key=`yillik-oran` |
| **Hesaplayıcı** | Yasal Faiz, Akdî Temerrüt (alt sınır), Menfi Tespit Faizi |
| **Kaynak** | BKK / Cumhurbaşkanı Kararı, Resmi Gazete |
| **Tarihçe** | 01.01.2006 → %9 (BKK 2005/9831, RG 30.12.2005/26039), 01.06.2024 → %24 (CBK 8485, RG 21.05.2024/32552) |
| **Seed satır sayısı** | 2 (1984-2005 arası tarihçe yok) |
| **Adım 2.17 düzeltmesi** | Önceki seed'de fabrike değerler vardı (2018, 2020 hayali oranlar). Resmi tarihçeye göre düzeltildi — sadece 2 BKK/CBK değişikliği var. |
| **Kontrol** | Yeni BKK/CBK çıktığında (resmi gazete RSS — Faz 5) |
| **Sorumlu** | manual — admin@lexcalculus.local (Faz 3'te admin paneli) |
| **Hatalı veri etkisi** | Yasal faiz, akdî faiz alt sınırı, menfi tespit faizi tüm hesapları yanlış |

---

## GLOBAL '*' PARAMETRELER — BACKFILL ORPHAN NOTU

Adım 2.20'de eklenen `FormulaParameterMetadataBackfill` map yapısı `ToolSlug` ile eşleşme yapar.
Aşağıdaki parametreler `ToolSlug = '*'` (wildcard) ile saklandığı için backfill kapsamı dışında kalır:

| ToolSlug / Key | Calculator(lar) |
|---|---|
| `*` / `damga-vergisi-orani` | Kıdem, İhbar, Yıllık İzin, Fazla Mesai, İşe İade |
| `*` / `yasal-faiz-orani-yillik` | Asgari Ücret |
| `*` / `asgari-ucret-brut` | Asgari Ücret + İş Hukuku alt sınır |

Bu satırların `ExpectedUpdateFrequency` ve `Notes` alanları NULL'dur. Calculator'lar parametreleri runtime'da
doğru okuyor — kullanım açısından sorun yok. Faz 3 admin panelinde manuel olarak doldurulacak.

---

## YAKLAŞAN ÖNEMLİ TARİHLER

### AYM K.2025/164 (22.07.2025)

| Alan | Değer |
|---|---|
| **Etki** | 3095 s.K. m.1 ve m.2 — sözleşmeden kaynaklanmayan borç ilişkileri için iptal |
| **Yürürlük** | 01.08.2026 |
| **Etkilenen calculator'lar** | Yasal Faiz, Akdî Temerrüt (alt sınır), Menfi Tespit Faizi |
| **Eylem** | O tarihte yasal düzenleme olmazsa hesaplayıcı davranışı gözden geçirilecek |
| **Şu an** | Calculator'lar 01.08.2026 sonrası tarihler için sarı uyarı gösteriyor |

---

## ÖZET — Önümüzdeki 12 Ay

| Tarih | Kontrol | Aksiyon |
|---|---|---|
| Her ayın 3'ü | TÜFE 12 ay ort. | Yeni `YYYY-MM` satırı ekle |
| 30 Haziran 2026 | TCMB avans yarı yıl | Değişiklik varsa yeni satır |
| Temmuz 2026 başı | Kıdem tazminatı tavanı 2026/2 | Yeni satır (Hazine genelgesi) |
| 1 Ağustos 2026 | AYM K.2025/164 yürürlük | Yasal Faiz / Menfi Tespit davranışını revize et |
| 31 Aralık 2026 | TCMB avans yıl sonu | Yeni yıl seed |
| Aralık 2026 - Ocak 2027 | Asgari ücret 2027 | Yeni satır |

---

## Faz 3'e Hazırlık

Faz 3 Adım 3.3 (Veri Tazelik Widget) için kullanılacak alanlar — şu an seed'de hangi parametre dolu:

| Kategori | Seed durumu | LastUpdatedDate var mı | ExpectedUpdateFrequency var mı |
|---|---|---|---|
| `tufe-12-ay-ort` | 64 satır | ✅ (announcement date) | ✅ "Monthly" |
| `tcmb-avans` | 18 satır | ❌ (backfill ToolSlug eşleşti, EffectiveDate kullanıldı) | ✅ "Biannual" |
| `kidem-tazminati` / `tavan` | 5 satır | ✅ 2024 satırlarında, eski 3'te yok | ✅ 2024'te "Biannual", eski 3'te yok |
| `yasal-faiz` | 2 satır | ❌ (backfill ToolSlug eşleşti, EffectiveDate kullanıldı) | ✅ "OnLawChange" |
| `*` / `*` parametreleri | — | ❌ orphan | ❌ orphan |

Faz 3 admin paneli açıldığında ilk iş bu eksik metadata'ları manuel doldurmak.

---

**Son güncelleme:** Adım 2.23 (legal-references.md ile birlikte rev edildi).
**Sorumlu:** Sistem Yöneticisi (toolshubteam@gmail.com).
**Sonraki revizyon:** Faz 3 Adım 3.3 sonunda (admin widget canlıya çıktığında bu manuel takip dosyası arşivlenecek).

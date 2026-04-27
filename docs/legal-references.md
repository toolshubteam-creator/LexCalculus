# Lex Calculus — Hukuki Referanslar Dokümanı

**Versiyon:** Faz 2 sonu (Adım 2.23)
**Son güncelleme:** 27 Nisan 2026
**Hedef kitle:** Geliştiriciler, hukuk danışmanları, denetçiler

Bu doküman Lex Calculus platformundaki 17 hesaplayıcının hukuki dayanaklarını,
formüllerini, parametre kaynaklarını, Faz 2 basitleştirmelerini ve Faz 3+ planlarını
özetler. Her hesaplayıcı için aynı şablonu izler.

---

## İçindekiler

1. [Parametre Mimarisi](#parametre-mimarisi)
2. [İş Hukuku (7)](#i̇ş-hukuku)
3. [Aktüerya (5)](#aktüerya)
4. [Faiz (5)](#faiz)
5. [Veri Olgunluğu Tablosu](#veri-olgunluğu-tablosu)
6. [Genel Faz 2 Basitleştirmeleri](#genel-faz-2-basitleştirmeleri)
7. [Faz 3+ Planlanan İyileştirmeler](#faz-3-planlanan-i̇yileştirmeler)

---

## Parametre Mimarisi

Sistem iki tür parametre kullanır:

**Tool-specific parametre** — Belli bir calculator'a özgü değerler.
DB'de `ToolSlug = '<calculator-slug>'` ile saklanır.
Örnek: `kidem-tazminati / tavan`, `tcmb-avans / yillik-oran`, `tufe-12-ay-ort / 2024-03`

**Global parametre** — Birden fazla calculator tarafından paylaşılan değerler.
DB'de `ToolSlug = '*'` (wildcard) ile saklanır.
Calculator'lar `_params.GetValueAsync("*", "<key>", date)` ile okur.

Aktif global parametreler:

| Key | Kullanan Calculator(lar) | Kaynak |
|---|---|---|
| `damga-vergisi-orani` | Kıdem, İhbar, Yıllık İzin, Fazla Mesai, İşe İade | 488 s.K. (binde 7,59 sabit) |
| `yasal-faiz-orani-yillik` | Asgari Ücret | 3095 s.K. (denetim için referans) |
| `asgari-ucret-brut` | Asgari Ücret + İş Hukuku alt sınır kontrolleri | Çalışma Bakanlığı yıllık genelgesi |

Bu pattern Faz 3 admin panelinde "global parametreler" bölümü olarak görselleştirilecek.

> **Not:** `FormulaParameterMetadataBackfill` mevcut implementasyonda `ToolSlug` ile
> eşleşme yapar. `*` prefix'li global parametreler otomatik backfill almaz; metadata
> alanları (`ExpectedUpdateFrequency`, `Notes`) NULL kalır. Faz 3 admin panelinde
> bu satırlar manuel olarak doldurulacak.

---

## İş Hukuku

### Kıdem Tazminatı (kidem-tazminati)

**LEGAL BASIS:**
- 4857 s.K. (mülga 1475 s.K. m.14)
- Ödenen tazminat: 30 gün giydirilmiş ücret per tam çalışma yılı
- Tavan: Dönemin yasal tavanı (Hazine ve Maliye Bakanlığı genelgesi)
- Damga vergisi: %0,759 (binde 7,59)

**FORMULA:**
- Giydirilmiş ücret = Brüt aylık ücret + aylık yan ödemeler
- Toplam gün / 365 = tam yıl + kalan gün (günlük basit oran)
- Hesaplama tabanı = min(giydirilmiş ücret, dönem tavanı)
- Brüt tazminat = hesaplama tabanı × (toplam gün / 365)
- Net tazminat = Brüt − damga vergisi

**PARAMETERS:**
- ToolSlug `kidem-tazminati` / Key `tavan` — Dönemsel kıdem tavanı
- ToolSlug `*` / Key `damga-vergisi-orani` — Damga vergisi oranı (%0,759)

**PHASE 2 SIMPLIFICATIONS:**
- Damga vergisi tek sabit oran; muhasebe basitleştirilmiş
- Yan ödemeler kullanıcı tarafından elle girilir; otomatik tasnif yok
- Fesih sebebine göre ayırım yok (haklı/haksız fesih aynı formül)

**PHASE 3+ PLANNED:**
- Fesih sebebine göre tavan indirimi kontrolleri
- Sabit yan ödemeler kataloğu

**STATUTE OF LIMITATIONS:** 4857 m.32 — 5 yıl

---

### İhbar Tazminatı (ihbar-tazminati)

**LEGAL BASIS:**
- 4857 s.K. m.17
- İhbar süreleri: 2 hafta (<6 ay), 4 hafta (6 ay-1,5 yıl), 6 hafta (1,5-3 yıl), 8 hafta (3+ yıl)
- Damga vergisi: %0,759
- Gelir vergisi: %15 sabit (Faz 2 basitleştirme)

**FORMULA:**
- Günlük ücret = Brüt aylık / 30
- Brüt tazminat = Günlük ücret × (ihbar süresi × 7)
- Net tazminat = Brüt − damga − gelir vergisi

**PARAMETERS:**
- ToolSlug `ihbar-tazminati` / Key `gelir-vergisi-orani-basit` — Gelir vergisi oranı (%15)
- ToolSlug `*` / Key `damga-vergisi-orani` — Damga vergisi (%0,759)

**PHASE 2 SIMPLIFICATIONS:**
- Sabit %15 gelir vergisi (gerçekte kümülatif yıllık kazanca bağlı)
- TİS / sözleşme ile uzatılmış ihbar süreleri desteklenmiyor
- Kötüniyet tazminatı (m.17/6) ayrı modülde değil

**PHASE 3+ PLANNED:**
- Kümülatif gelir vergisi tablosu
- TİS özel ihbar süresi alanı
- Kötüniyet tazminatı 3x katsayı opsiyonu

**STATUTE OF LIMITATIONS:** 4857 m.32 — 5 yıl

---

### Yıllık İzin Ücreti (yillik-izin-ucreti)

**LEGAL BASIS:**
- 4857 s.K. m.53
- Yıllık izin hakkı: 14 gün (1-5 yıl), 20 gün (5-15 yıl), 26 gün (15+ yıl)
- Yaş özel hükmü (m.53/4): 18 yaş altı ve 50+ yaş = minimum 20 gün
- Damga vergisi: %0,759 / Gelir vergisi: %15 (Faz 2)

**FORMULA:**
- Tam yıl = Toplam gün / 365
- Yıllık izin hakkı = tam yıl × günlük (14/20/26)
- Kullanılmayan izin = Toplam hak − Kullanılan izin (min 0)
- Brüt ücret = (Brüt aylık / 30) × Kullanılmayan izin
- Net = Brüt − damga − gelir vergisi

**PARAMETERS:**
- ToolSlug `yillik-izin-ucreti` / Key `gelir-vergisi-orani-basit` (fallback `ihbar-tazminati`)
- ToolSlug `*` / Key `damga-vergisi-orani`

**PHASE 2 SIMPLIFICATIONS:**
- Sabit %15 gelir vergisi (tek dilim)
- Yaş özel hükmü manuel doğum tarihi kontrolü
- Kısmi yıl tam yıl olarak işlenmiş; günlük orantılı ödeme yok

**PHASE 3+ PLANNED:**
- Dönem başına değişken izin günü (kontrata bağlı)
- Gelir vergisi tablosu entegrasyonu

**STATUTE OF LIMITATIONS:** 4857 m.59 — 5 yıl

---

### Fazla Mesai Alacağı (fazla-mesai)

**LEGAL BASIS:**
- 4857 s.K. m.41
- Fazla mesai (45 saat üzeri): %50 zamlı (1,5×)
- Hafta tatili / Ulusal bayram: %100 zamlı (2×)
- Damga vergisi: %0,759 / Gelir vergisi: %15 (Faz 2)

**FORMULA:**
- Saatlik ücret = Brüt aylık / (haftalık saat × 4,33)
- Fazla mesai = Saatlik × saat × 1,5
- Hafta tatili / Bayram = Saatlik × saat × 2,0
- Brüt toplam = Σ (kategori tutarları)
- Net = Brüt − damga − gelir vergisi

**PARAMETERS:**
- ToolSlug `fazla-mesai` / Key `gelir-vergisi-orani-basit` (fallback `ihbar-tazminati`)
- ToolSlug `*` / Key `damga-vergisi-orani`

**PHASE 2 SIMPLIFICATIONS:**
- Sabit %15 gelir vergisi
- 4,33 sabit ay/hafta oranı (52/12)
- Kategorisel zam oranları değiştirilemez

**PHASE 3+ PLANNED:**
- Kümülatif gelir vergisi tablosu
- Dönemsel zam oranları (kanuni değişimler)
- Periyodik mesai raporları

**STATUTE OF LIMITATIONS:** 4857 m.32 — 5 yıl

---

### İşe İade Tazminatı (ise-iade-tazminati)

**LEGAL BASIS:**
- 4857 s.K. m.18-21
- İş güvencesi şartı: 30+ işçi, 6+ ay kıdem, belirsiz süreli, işletme dışı sebep
- Tazminat: 4-8 ay brüt ücret (mahkeme takdiri)
- Boşta geçen süre ücreti: max 4 ay
- Damga vergisi: %0,759 / Gelir vergisi: %15 (Faz 2)

**FORMULA:**
- İade tazminatı = Brüt aylık × belirlenen ay (4-8)
- Boşta ücreti = Brüt aylık × min(boşta ay, 4)
- Net = (İade + Boşta) − damga − gelir vergisi

**PARAMETERS:**
- ToolSlug `ise-iade-tazminati` / Key `gelir-vergisi-orani-basit` (fallback `ihbar-tazminati`)
- ToolSlug `*` / Key `damga-vergisi-orani`

**PHASE 2 SIMPLIFICATIONS:**
- Tazminat ay sayısı (4-8) kullanıcı girer (mahkeme takdiri)
- Boşta süre 4 ay sınırı kesin
- İş güvencesi şartı kontrolü bilgilendirme amaçlı

**PHASE 3+ PLANNED:**
- Mahkeme takdir orta noktası simülatörü
- Davaya konu sebep tasnifi (haklı/haksız fesih)
- Borçlandırma takvimi

**STATUTE OF LIMITATIONS:** 4857 m.20 — 1 ay (fesih bildiriminden itibaren dava açma süresi)

---

### Asgari Ücret Uyumluluk Kontrolü (asgari-ucret-kontrol)

**LEGAL BASIS:**
- Çalışma Bakanlığı dönemsel asgari ücret yayınları (Aralık-Ocak)
- 4857 s.K. (genel mevzuat tabanı)
- 3095 s.K. m.1 (yasal faiz oranı, denetim için referans)

**FORMULA:**
- Her ay için: Asgari ücret − ödenen brüt = aylık eksiklik (min 0)
- Toplam eksik = Σ aylık eksiklik
- Yasal faiz = Σ (ay eksikliği × yıllar/12 × faiz oranı)
- Toplam alacak = Toplam eksik + Yasal faiz

**PARAMETERS:**
- ToolSlug `*` / Key `asgari-ucret-brut` — Dönemsel asgari brüt
- ToolSlug `*` / Key `yasal-faiz-orani-yillik` — Yasal faiz oranı (%18)

**PHASE 2 SIMPLIFICATIONS:**
- Sabit brüt ücret (dönem içi zam yok)
- Sabit %18 yıllık faiz oranı (3095 m.1 dönem tablosu yerine)
- Aylık granülarite (kısmi ay proration yok)

**PHASE 3+ PLANNED:**
- Ay başına ücret girişi (dönem içi zam)
- Dönemsel yasal faiz oranı tablosu (3095)
- Kısmi ay proorate

**STATUTE OF LIMITATIONS:** 4857 m.32 — 5 yıl

---

### Mobbing / Manevi Tazminat (mobbing-tazminati)

**LEGAL BASIS:**
- TBK m.58 (manevi zarar tazminatı)
- 4857 s.K. m.5, m.77 (İş Hukuku alan)
- Yargıtay 9. Hukuk Dairesi emsal kararları

**FORMULA:**
- Taban ay katsayısı (şiddete göre): Hafif 1-3 ay, Orta 3-6 ay, Ağır 6-12 ay, Çok Ağır 12-24 ay
- Düzeltmeler: +2 ay (≥24 ay süre), +3 ay (sağlık raporu), +2 ay (mobbing sebepli istifa),
  +1 ay (büyük holding), +1 ay (kamu kurumu)
- Alt sınır = Brüt × taban ay
- Üst sınır = Brüt × üst ay
- Önerilen = (Alt + Üst) / 2 + (Üst − Alt) × 0,20

**PARAMETERS:** yok (Yargıtay emsal kararlarına dayalı)

**PHASE 2 SIMPLIFICATIONS:**
- Kanuni formül yok; saf Yargıtay emsal ortalama tahmini
- Kötüniyet tespiti yok; mahkeme kararı gerekir
- Sistemli süreklilik kanıtı: 6 ay süre kontrolü tek ölçüt

**PHASE 3+ PLANNED:**
- Yargıtay karar arşivi entegrasyonu
- Sağlık raporu tipi detaylaştırması (psikiyatri, depresyon vb.)
- Bölgesel/daire bazında emsal oranı

**STATUTE OF LIMITATIONS:** TBK m.72 — 1 yıl (zarar ve fail öğreniminden) / 10 yıl genel

---

## Aktüerya

### Destekten Yoksun Kalma Tazminatı (destekten-yoksun-kalma)

**LEGAL BASIS:**
- TBK m.53 (haksız fiilden doğan zarar)
- Yargıtay HGK aile paylaşım kuralları
- TRH 2010 (Türkiye Hayat Tablosu) — yaşam beklentisi

**FORMULA:**
- Aktif dönem: Şu an yaş → 65 yaş (tam gelir)
- Pasif dönem: 65 yaş → beklenen ömür sonu (%50 gelir)
- Aile payı (Yargıtay HGK):
  - Eş yalnız: %50
  - Eş + 1 çocuk: %25 / %25
  - Eş + 2 çocuk: %20 / %20 / %20
  - Eş + 3 çocuk: %15 (her biri)
  - Eş + 4+ çocuk: capped (toplam ≤ %75)
  - Çocuk yalnız: toplam %50, eşit paylaş
- Çocuk destek süresi: Erkek 18, Kız 22, Öğrenci 25 (ölenin kalan ömrü ile sınırlı)
- PV = Yıllık gelir pay × Annuity(dönem, iskonto)

**PARAMETERS:**
- ILifeTableService (TRH 2010)
- IActuarialService (iskonto, annuity)

**PHASE 2 SIMPLIFICATIONS:**
- Pasif dönem sabit %50 gelir oranı
- İskonto oranı: User input (varsayılan ~%1.8)
- Yargıtay HGK kuralları sabit (güncel değişimler takip edilmez)

**PHASE 3+ PLANNED:**
- Sosyal güvenlik yardım tasnifi (destek vs. emeklilik)
- Dönemsel iskonto oranı tablosu
- Yaşam tablosu güncelleme otomasyonu

**STATUTE OF LIMITATIONS:** TBK m.72 — 2 yıl / 10 yıl

---

### Maluliyet Tazminatı (maluliyet-tazminati)

**LEGAL BASIS:**
- TBK m.54 (haksız fiilden doğan zarar — engelli kalan kişi)
- 5510 s.K. (SGK ve iş gücü kaybı oranı belirlemesi)
- TRH 2010 (yaşam beklentisi)

**FORMULA:**
- Aktif dönem: Şu an yaş → 65 yaş
- Pasif dönem: 65 yaş → beklenen ömür sonu (%50 gelir)
- Yıllık kayıp = Yıllık gelir × İş gücü kaybı oranı (%)
- Aktif PV = Yıllık kayıp × Annuity(aktif yıl, iskonto)
- Pasif PV = Yıllık kayıp × %50 × Annuity(pasif yıl, iskonto)
- Toplam = Aktif PV + Pasif PV

**PARAMETERS:**
- ILifeTableService (TRH 2010)
- IActuarialService (iskonto, annuity)

**PHASE 2 SIMPLIFICATIONS:**
- Sabit %50 pasif dönem gelir oranı
- İş gücü kaybı oranı user input (Adli Tıp/SGK raporu)
- İskonto oranı user input

**PHASE 3+ PLANNED:**
- SGK tıbbi sınıflama tasnifi
- Yaşam tablosu güncelleme
- Dönemsel iskonto oranı tablosu

**STATUTE OF LIMITATIONS:** TBK m.72 — 2 yıl / 10 yıl

---

### Geçici İş Göremezlik Tazminatı (gecici-is-goremezlik)

**LEGAL BASIS:**
- 5510 s.K. m.18 (SGK geçici iş göremezlik ödeneği — %66,67)
- TBK m.54 (sorumlu tarafın tamamlayıcı sorumluluğu)

**FORMULA:**
- Günlük brüt = Aylık brüt / 30
- Brüt mahrum = Günlük brüt × gün sayısı
- SGK ödeneği = Brüt mahrum × %66,67
- Net talep = Brüt mahrum − SGK ödeneği (mahsup aktifse)

**PARAMETERS:** yok (SGK oranı %66,67 sabit; mahsup toggle user input)

**PHASE 2 SIMPLIFICATIONS:**
- SGK ödeneği %66,67 sabit
- Günlük basit takvim (30 gün/ay)
- Kayıt dışı çalışma: User toggle ile SGK mahsupu devre dışı

**PHASE 3+ PLANNED:**
- SGK ödeme durumu otomatik karşılaştırma
- Emeklilik/engelli durumu kontrolleri
- Dönemsel kanun değişimleri

**STATUTE OF LIMITATIONS:** TBK m.72 — 2 yıl

---

### Bakıcı Gideri Tazminatı (bakici-gideri)

**LEGAL BASIS:**
- TBK m.54 (haksız fiilden doğan ek zararlar — bakım/refakat)
- Adli Tıp / Sağlık Kurulu raporları (bakım ihtiyaç oranı)

**FORMULA:**
- Aylık efektif maliyet = Aylık bakıcı maliyeti × Bakım oranı (%)
- Yıllık maliyet = Aylık efektif × 12
- Toplam PV = Yıllık × Annuity(yaşam yılı, iskonto)

**PARAMETERS:**
- ILifeTableService (TRH 2010)
- IActuarialService (iskonto, annuity)

**PHASE 2 SIMPLIFICATIONS:**
- Bakım oranı: User input (Sağlık Kurulu raporu)
- Sabit aylık maliyet (dönem içi artış yok)
- İskonto oranı: User input

**PHASE 3+ PLANNED:**
- Bakım oranı tipine göre standart tasnif (6 saat = %25, 24 saat = %100)
- Bakıcı ücret endeksasyonu
- Yaşam tablosu güncelleme

**STATUTE OF LIMITATIONS:** TBK m.72 — 2 yıl / 10 yıl

---

### Araç Değer Kaybı (arac-deger-kaybi)

**LEGAL BASIS:**
- TBK m.49 (maliyet zararı)
- Yargıtay 17. Hukuk Dairesi kararları
- Sigorta Tahkim Komisyonu (KTK) araç değerlemesi (TRAMER yöntemi)

**FORMULA (TRAMER):**
- Hasar oranı = (Kazadan önceki − Kazadan sonraki) / Kazadan önceki
- Yaş faktörü: 0-1 yıl 1.0, 2 yıl 0.85, 3 yıl 0.70, 4-5 yıl 0.50, 6-10 yıl 0.30, 11+ yıl 0.10
- Km faktörü: <30k 1.0, 30-100k 0.80, 100-200k 0.50, ≥200k 0.20
- Değer kaybı = Kazadan önceki × Hasar oranı × Yaş faktörü × Km faktörü
- Pert riski: Hasar oranı > %30 → uyarı

**PARAMETERS:** yok (faktör tabloları sabit)

**PHASE 2 SIMPLIFICATIONS:**
- TRAMER baseline yöntemi (Sigorta Bilirkişiler Derneği standardı)
- Pert %30 eşiği kesin
- Tamir maliyeti vs. değer kaybı ayrımı yok

**PHASE 3+ PLANNED:**
- Tamir maliyeti ile kıyaslama
- Geri alım tarihi ve müzayede değer kontrolleri
- Marka/model spesifik depreciation tablosu

**STATUTE OF LIMITATIONS:** TBK m.72 — 2 yıl / 10 yıl

---

## Faiz

### Yasal Faiz (yasal-faiz)

**LEGAL BASIS:**
- 3095 s.K. m.1 (yasal faiz oranı tarihçesi)
- 3095 s.K. m.3 (mürekkep faiz yasağı)
- AYM K.2025/164 (22.07.2025): 3095 m.1 sözleşmeden kaynaklanmayan borçlar için iptal (yürürlük: 01.08.2026)

**FORMULA:**
- Basit faiz = Ana para × Yıllık oran × (Gün / Yıl bazı [365])
- Dönem bazında: Oran değişikliklerinde her dönem ayrı hesap, sonra toplanır
- Toplam tutar = Ana para + Σ(dönem faiz)

**PARAMETERS:**
- IInterestRateService → ToolSlug `yasal-faiz` / Key `yillik-oran` (01.01.2006 %9, 01.06.2024 %24)

**PHASE 2 SIMPLIFICATIONS:**
- Basit faiz (mürekkep yasak — m.3)
- Dönemsel tablo: 2 satır (1984-2006 öncesi yok)
- Gün/yıl bazı: 365 (Yargıtay standardı), 360 opsiyonu mevcut

**PHASE 3+ PLANNED:**
- 1984-2005 arası tarihçe seed
- Yasal faiz oranı tablosunun otomatik güncellenmesi (Resmi Gazete RSS)

**STATUTE OF LIMITATIONS:** Ana alacağa bağlı (faiz bağımsız zamanaşımına tabi değil)

---

### Ticari Temerrüt Faizi (ticari-temerrut-faizi)

**LEGAL BASIS:**
- 3095 s.K. m.2 (ticari işlerde — yasal faiz vs. TCMB avans, yüksek olanı seçilebilir)
- 3095 s.K. m.3 (mürekkep faiz yasağı)
- 6 aylık dönem kuralı + 5 puan kuralı
- AYM K.2025/164: m.2 sözleşmeden kaynaklanmayan borçlar yönünden iptal (yürürlük: 01.08.2026)

**FORMULA:**
- Yasal faiz hesabı (3095 m.1)
- Ticari temerrüt faizi: TCMB avans × 6 aylık dönem (5 puan kuralı)
- Uygulanacak = max(Yasal, Ticari) — alacaklı seçer
- Toplam tutar = Ana para + Uygulanacak faiz

**PARAMETERS:**
- IInterestRateService (yasal-faiz)
- IThree095CommercialRateService → ToolSlug `tcmb-avans` / Key `yillik-oran` (18 satır seed: 2018-06-29 → 2025-12-20)

**PHASE 2 SIMPLIFICATIONS:**
- TCMB avans oranı: Sadece 31 Aralık + 30 Haziran snapshot'ları (yıl içi diğer değişimler yok sayılır — m.2 emredici)
- 5 puan kuralı sabit uygulandı
- Pre-2018 dönemler için seed eksik

**PHASE 3+ PLANNED:**
- TCMB API entegrasyonu (otomatik avans oranı)
- Ticari işletme tanımı kontrolleri (TTK m.12)
- Pre-2018 tarihçe seed

**STATUTE OF LIMITATIONS:** Ana alacağa bağlı

---

### Akdî Temerrüt Faizi (akdi-temerrut-faizi)

**LEGAL BASIS:**
- TBK m.120 (sözleşmede belirlenen temerrüt faizi)
- TBK m.120/2 alt sınır kuralı: Sözleşme oranı 3095 m.2 oranından az olamaz
- TBK m.27 (ahlaka aykırı sözleşme — %100+ yıllık ortalama oranda uyarı)
- 3095 s.K. m.2 (yasal alt sınır), m.3 (mürekkep faiz yasağı, TTK istisnası)

**FORMULA:**
- Akdî hesap: Sözleşme oranları (basit veya bileşik — TTK istisnası)
- Yasal alt sınır paralel hesabı: 3095 m.2
- Uygulanacak = max(Akdî, Yasal alt sınır)
- Bileşik faiz: Sadece TTK kapsamında tacirler arası yazılı sözleşmede; aksi halde basit + uyarı

**PARAMETERS:**
- User input: Sözleşme dönem oranları (`SozlesmeOranDonem` listesi)
- IThree095CommercialRateService (3095 m.2 alt sınır)

**PHASE 2 SIMPLIFICATIONS:**
- Tacir vs. tüccar tanımı user checkbox (otomatik kontrol yok)
- TBK m.27 eşiği %100 sabit; mahkeme indirim formula yok
- Bileşik dönem opsiyonları: Aylık/3-aylık/6-aylık/yıllık

**PHASE 3+ PLANNED:**
- Tacir tanımı otomatik kontrolü (vergi numarası entegrasyonu)
- TBK m.27 indirim formula entegrasyonu
- Sektörel standart faiz oranları kataloğu

**STATUTE OF LIMITATIONS:** Ana alacağa bağlı

---

### Kira Artış Tespiti (kira-artisi)

**LEGAL BASIS:**
- TBK m.344/1 (18.01.2019 değişikliği): TÜFE 12 aylık ortalama üst sınır
- 7409 s.K. (RG 11.06.2022 + uzatma): Konut 11.06.2022-01.07.2024 azami %25
- 01.07.2020'den itibaren çatılı işyeri konutla aynı (TÜFE üst sınırı)
- TBK m.344/3: 5+ yıl sözleşme süresi → kira tespit davası

**FORMULA:**
- TÜFE oranı = Yenileme tarihinden bir önceki ayın 12 aylık ortalama (TÜİK)
- Sözleşme oranı varsa: min(sözleşme, TÜFE)
- Konut + 11.06.2022-01.07.2024: min(yukarıdaki, %25)
- İşyeri için %25 sınırı YOK
- Yeni kira = Mevcut kira × (1 + uygulanacak / 100)

**PARAMETERS:**
- ITUFEService → ToolSlug `tufe-12-ay-ort` / Key `YYYY-MM` (64 satır seed: 2020-12 → 2026-03)
- Override: Manuel TÜFE girişi (sistem dışı aylar)

**PHASE 2 SIMPLIFICATIONS:**
- TÜFE verisi manuel update; otomatik TÜİK API yok
- %25 konut sınırı 11.06.2022 - 01.07.2024 kesin tarih
- Sözleşme koşul tasnifi yok

**PHASE 3+ PLANNED:**
- TÜİK API otomatik entegrasyonu
- Sözleşme koşul tasnifi (ayarlı vs. sabit oran)
- 5 yıl kira tespit davası tetiği

**STATUTE OF LIMITATIONS:** TBK m.344/3 — 5+ yıl sözleşmede kira tespit davası açılabilir

---

### Menfi Tespit Faizi (menfi-tespit-faizi)

**LEGAL BASIS:**
- TBK m.78 (sebepsiz zenginleşmeden istirdat)
- TBK m.79 (iyiniyet/kötüniyet ayrımı):
  - Kötüniyetli: Tahsil tarihinden faiz (m.79/2)
  - İyiniyetli: İade talep tarihinden faiz (m.79/1)
- 3095 s.K. m.1 (uygulanacak yasal faiz oranı)
- AYM K.2025/164: 3095 m.1 sözleşmeden kaynaklanmayan borçlar için iptal (yürürlük: 01.08.2026 — sebepsiz zenginleşme bu kapsamda)

**FORMULA:**
- Faiz başlangıç: Kötüniyet → tahsil; iyiniyet → iade talep
- Basit faiz = Tutar × Dönemsel oran × (Gün / Yıl bazı)
- Toplam iade = Tutar + Faiz

**PARAMETERS:**
- IInterestRateService (Yasal Faiz seed paylaşımı)

**PHASE 2 SIMPLIFICATIONS:**
- Kötüniyet/iyiniyet user choice (mahkeme kanıtlaması gerekir)
- Sebepsiz zenginleşme tipi tasnifi yok (genel hesap)
- Mahkeme harç ve vekalet ücreti dahil değil

**PHASE 3+ PLANNED:**
- Kötüniyet kanıt tasnifi (belge/mesaj kategorileri)
- Sebepsiz zenginleşme tipi kataloğu
- "Bilmesi gerektiği" testi (subjektif/objektif iyiniyet)

**STATUTE OF LIMITATIONS:** TBK m.82 — 2 yıl (öğrenmeden) / 10 yıl (zenginleşmeden) genel

---

## Veri Olgunluğu Tablosu

Aşağıdaki tablo her calculator'ın hangi tarihten itibaren güvenilir hesap yaptığını gösterir.
Daha eski tarihler için seed'de veri yok; calculator hata verecek veya yaklaşık değer döndürecek.

| Calculator | Güvenilir Başlangıç | Eksik Veri Açıklaması |
|---|---|---|
| Kıdem Tazminatı | 2024-01-01 | 2023 ve öncesi tavan seed'de yok (Faz 3'te eklenecek) |
| İhbar Tazminatı | Tüm tarihler | Sabit kanun, dönem bağımsız |
| Yıllık İzin | Tüm tarihler | Sabit kanun |
| Fazla Mesai | Tüm tarihler | Sabit kanun + brüt ücret |
| İşe İade | Tüm tarihler | Mahkeme takdiri |
| Asgari Ücret | 2024-01-01 | 2023 ve öncesi seed'de yok |
| Mobbing | Tüm tarihler | TBK m.58 takdir |
| Aktüerya 5 (Destek/Maluliyet/Bakıcı/Geçici/Araç) | Tüm tarihler | TRH 2010 LifeTable, 100+ yıl kapsar |
| Yasal Faiz | 2006-01-01 | 3095 m.1 sadece 2 satır (1984-2005 yok) |
| Ticari Temerrüt | 2018-06-29 | TCMB avans pre-2018 satırları eksik |
| Akdî Temerrüt | 2006-01-01 | Yasal Faiz altyapısı paylaşır |
| Kira Artışı | 2021-01-01 | TÜFE 12 ay ort. 2020-12'den itibaren (yenileme tarihi 2021-01+ olmalı) |
| Menfi Tespit | 2006-01-01 | Yasal Faiz altyapısı paylaşır |

---

## Genel Faz 2 Basitleştirmeleri

Tüm calculator'lar genelinde geçerli basitleştirmeler:

- **Brüt-Net dönüşümü:** Sabit %15 gelir vergisi varsayımı; gerçek SGK/işsizlik primi/AGİ hesabı yok
- **İskonto oranı:** Aktüeryal hesaplarda kullanıcı girdisi; TCMB referans entegrasyonu Faz 3
- **Tarih hassasiyeti:** Hesaplar gün hassasiyetinde; saat/dakika dikkate alınmaz
- **Para birimi:** Sadece TL; döviz hesabı yok
- **Yargıtay içtihat değişiklikleri:** Manuel kod güncellemesi gerektirir; içtihat veritabanı yok
- **AYM K.2025/164 etkisi:** Akdî Temerrüt ve Menfi Tespit'te uyarı var, hesap üretilmeye devam ediyor (kullanıcı kararına bırakıldı)
- **Backfill metadata:** `*` prefix'li global parametreler `FormulaParameterMetadataBackfill` tarafından eşleştirilmez (Faz 3 admin paneli manuel doldurur)

---

## Faz 3+ Planlanan İyileştirmeler

**Faz 3 — Admin Paneli & Hesap Geçmişi (kesin):**
- Tüm parametreler için CRUD admin paneli (FormulaParameters, LifeTableRows)
- Kullanıcı hesap geçmişi UI (Adım 2.22'de altyapı yazıldı, şimdi okuma tarafı)
- Veri tazelik widget'ı (TÜFE/TCMB/Kıdem tavanı gecikme uyarıları)
- Multi-tenant altyapı (organizasyon/firma kavramı)

**Faz 4 — Sosyal Platform & Plan Sistemi (planlı):**
- Kullanıcı plan sistemi (free/pro/enterprise)
- E-posta bildirim sistemi (cron job ile veri tazelik uyarısı)
- Mesleki bağlantılar, mesajlaşma (SignalR)

**Faz 5 — Otomatik Veri Çekme (opsiyonel):**
- TÜİK API entegrasyonu (TÜFE otomatik)
- TCMB API (avans faizi otomatik)
- Hazine ve Maliye Bakanlığı sayfa scraping (kıdem tavanı)
- Resmi Gazete RSS (mevzuat değişiklik takibi)

---

**Doküman versiyonu:** 1.0 (27.04.2026, Adım 2.23)
**Bakım sorumlusu:** Sistem Yöneticisi (toolshubteam@gmail.com)
**Sonraki güncelleme:** Faz 3 başlangıcında calculator metadata değişikliklerine göre revize edilecek.

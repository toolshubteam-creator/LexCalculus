# Lex Calculus — Faz 7 Charter
## Hesaplama Tamamlama

**Başlangıç:** 5 Haziran 2026
**Tahmin:** 3-4 hafta charter, ~13-16 gün gerçek (Faz 6 baseline)
**Önceki:** Faz 6 tamamlandı (2 Haziran 2026, ~5 gün gerçek)
**Tag (kapanış):** `phase-7-complete`
**Charter sürümü:** 1.0
**Temel:** `docs/phase-7-scope-inventory.md` (Adım 7.0 denetim envanteri)

---

## §1 Amaç ve Kapsam

Faz 7 Lex Calculus'un **ana ürününü tamamlar**: D-I kategorileri için 26 yeni
hesaplama aracı. Faz 2'de A-C kategorileri (17 araç) tamamlanmıştı; D-I
"Entegrasyonlar ve Kalan Araçlar" başlığı altında Faz 5'e bırakılmış,
Faz 3-6 sosyal platform ve olgunlaştırma ile geçmişti.

Bu erteleme Adım 7.0 envanter denetiminde sorgulandı: "Hukuki hesaplama
platformu" satış argümanı 17 araçla zayıf, 32-43 araçla kapsamlı. Faz 6
sonu platformu üretim hazır olgunluğa taşıdı; Faz 7 ana ürünü tamamlar.

`phase-7-scope-inventory.md` (Adım 7.0) envanter temeli:
- Kesin 26 yeni araç (§6 Tam Katalog otorite, Ek A güvensiz)
- 6 kategori: D Gayrimenkul (5), E Aile/Miras (4), F Ceza (5),
  G Vergi/İdare (5), H Ticaret (3), I Bilirkişi (4)
- Karmaşıklık: BASİT 5 / ORTA 12 / KARMAŞIK 9
- Mevcut altyapı (Faz 2) tam reuse: ICalculator, FormulaParameter,
  CalculationHistory, view + test pattern

Faz 7 sonunda Lex Calculus:
- 43 aktif hesaplama aracı (Faz 2 + Faz 7 = 17 + 26)
- 9 kategori tam kapsanmış
- Lansman öncesi tam ürün

---

## §2 Vizyon Tutarlılığı

Faz 1-6 temel kararlar korunur:
- **Plansız (Free)** — yeni araçlar ek ücretsiz erişilebilir
- **Vatandaş 1. sınıf** — UI/dil basit, hukukçu olmayan da kullanabilir
- **KVKK uyumlu** — hesaplama geçmişi anonimize ile silinir (mevcut)
- **Mevzuat doğruluğu** — her hesap referans karar veya emsal ile doğrulanır

---

## §3 Mimari Kararlar (Yeni)

### Karar 1 — Vergi dilim altyapısı

G1 (Veraset Vergisi) ve G5 (Vergi Cezası) için dilim tablosu gerekli.
Çözüm: Yeni entity `TaxBracket` (`Id`, `ToolSlug`, `MinAmount`, `MaxAmount`,
`Rate`, `EffectiveDate`). FormulaParameter JSON yerine ayrı entity — sorgu
kolay, tarih bazlı versiyonlama net, admin UI gelecekte basit.

### Karar 2 — Ceza takvim altyapısı

F1 (Erteleme), F2 (Koşullu Salıverilme), F3 (Zamanaşımı), F5 (Tutukluluk
Mahsup) için gün/ay/yıl hesabı + tahliye tarihi. Çözüm:
`ICriminalCalendarService` (gün ekle/çıkar, hafta sonu/resmi tatil dahil
veya hariç hesap, infaz süresi). Türkiye resmi tatil takvimi sabit liste
(Faz 7'de seed).

### Karar 3 — Miras dağıtım altyapısı

E2 (Miras Payı) ve E3 (Tenkis) için mirasçı derecesi + saklı pay
algoritması. Çözüm: `IInheritanceDistributionService` (TMK m.495-501 derece
hiyerarşisi, saklı pay yüzdeleri, tenkis hesabı). Yeni entity yok, algoritma
servisi. E3 tenkis E2'ye bağımlı; E2 önce.

### Karar 4 — Referans karar test pattern

Karmaşık araçlar (9 adet) için birim test yanında **referans karar test'i**
zorunlu. Pattern Faz 2'de Kıdem için uygulanmıştı:
- Test: "Yargıtay [karar no] davasındaki değerlerle hesap → X TL bulundu,
  Yargıtay X TL onayladı, hesap X TL ± %1 olmalı"
- Mevzuat değişikliği → referans yenilenir
- Test dosyası: `{Calculator}ReferenceCaseTests.cs`

---

## §4 Adım Kırılımı (3 Dalga, 12 Alt Adım)

### Dalga A — Gayrimenkul + Aile/Miras (Adım 7.2-7.6, 9 araç)

- **7.2** D Gayrimenkul altyapı + D1 Arsa Payı (BASİT)
- **7.3** D2 Kamulaştırma Bedeli (KARMAŞIK) + D3 Ecrimisil (ORTA)
- **7.4** D4 Kat Karşılığı (ORTA) + D5 Hâsılat Kira (ORTA)
- **7.5** E Aile/Miras altyapı + E1 Nafaka (ORTA) + E4 Mal Rejimi (ORTA)
- **7.6** Miras dağıtım servisi + E2 Miras Payı (KARMAŞIK) + E3 Tenkis (KARMAŞIK)

Dalga A closeout, 7.6 adımı sonunda mini doküman güncelleme dahil.
Tahmin: ~5-6 gün.

### Dalga B — Ceza + Vergi/İdare (Adım 7.7-7.10, 10 araç)

- **7.7** Ceza takvim servisi + F1 Erteleme (ORTA) + F2 Koşullu Salıverilme (KARMAŞIK)
- **7.8** F3 Zamanaşımı (KARMAŞIK) + F4 Adli Para Cezası (ORTA) + F5 Tutukluluk Mahsup (BASİT)
- **7.9** Vergi dilim altyapı + G1 Veraset (KARMAŞIK) + G2 Tapu Harcı (BASİT)
- **7.10** G3 Damga Vergisi (ORTA) + G4 KDV İade (ORTA) + G5 Vergi Cezası (KARMAŞIK)

Dalga B closeout, 7.10 sonunda.
Tahmin: ~5-6 gün.

### Dalga C — Ticaret + Bilirkişi + closeout (Adım 7.11-7.13, 7 araç)

- **7.11** H1 Şirket Tasfiye Payı (ORTA) + H2 Kâr Payı (ORTA) + H3 Sözleşme Cezası (ORTA)
- **7.12** I1 PMF Sorgulama (BASİT, LifeTable reuse) + I2 İskontolu Nakit Akışı (BASİT,
  Actuarial reuse) + I3 Hakkaniyetli Tazminat (KARMAŞIK) + I4 Çevresel Zarar (ORTA)
- **7.13** Faz 7 closeout (Faz 6 closeout pattern)

Tahmin: ~3-4 gün.

---

## §5 Tahmin

Charter: 3-4 hafta.
Gerçek Faz 6 baseline: 5 gün (charter 4-5 hafta tahmin).

Faz 7 hesaplama tamamlama — araç başına ~30-45 dk (altyapı hazır), ama
KARMAŞIK 9 araç ek zaman (mevzuat araştırma, referans karar test) →
~15 dk/araç fazla.

Muhafazakar tahmin: **13-16 gün** (~2-3 hafta).
Optimist: 10-12 gün.

**Muhafazakar baseline notu:** Faz 4 charter 13 hafta gerçek 2 gün (~32x),
Faz 5 6 hafta gerçek 2 hafta, Faz 6 4-5 hafta gerçek 5 gün. Tahminler
sürekli düşmüş — Faz 7 da bu eğilimi sürdürebilir, ama hesaplama
karmaşıklığı + mevzuat doğruluğu zorunluluğu temkin gerektirir.

---

## §6 Risk ve Tuzaklar

### Mevzuat doğruluğu (KRİTİK)

Hatalı hesap = avukat güvenini kaybetmek + hukuki sorumluluk riski.
- Her KARMAŞIK araç için referans karar test zorunlu (Karar 4)
- Mevzuat değişiklikleri (TBK, TMK, TCK, 4857, vs.) Faz 7 boyunca takip
- Faz 7 sonu **hukuk profesyoneli incelemesi** önerilir (Faz 2'de de bahsedildi)

### Vergi dilim tablosu kaynak güvenilirliği

G1 Veraset Vergisi 2024-2026 dilim tutarları:
- Resmi Gazete / Maliye Bakanlığı sayfası
- Otomatik scraping yok (Faz 8+ entegrasyon), elle seed

### Ceza takvim edge case'leri

F2 Koşullu Salıverilme: suç tipine göre 2/3 veya 3/4 oran. Suç tipi listesi
TCK + Terör + Cinsel + Diğer şeklinde kaba kırılım. Yargıtay içtihat'a göre
oran sapması olabilir — basitleştirilmiş model + uyarı metni.

### Miras tenkis karmaşıklığı

E3 Tenkis algoritması TMK m.560 + içtihat. Basitleştirme yapılırsa avukatı
yanıltır. Çözüm: önce algoritma akış şeması, Adım 7.6'da kapsamlı test.

### Referans belge süreci (tech-debt #42)

Adım 7.0'da fark edildi: teknik rapor dangling commit'te kalmış, main'e
merge edilmemişti. Faz 7 sırasında benzer dokümanlar (referans karar
örnekleri, mevzuat değişiklik logları) ana branch'te olmalı.

---

## §7 Test Stratejisi

- Mevcut SQL Server LocalDB altyapı (Adım 5.8) kullanılır
- **Birim test:** her calculator için Input → Output doğrulama (5+ test)
- **Referans karar test:** KARMAŞIK 9 araç için zorunlu (Karar 4)
- **Parametre seed test:** idempotent (Faz 2 pattern)
- **View render test:** Razor template çalışıyor mu (mevcut pattern)

Hedef test sayım: 838 → ~1000+ (Faz 7 sonu, 26 araç × ~6 test = +156)

---

## §8 Tamamlanma Kriterleri

Faz 7 tamamlandı sayılır:
1. 26 yeni calculator aktif (placeholder yok)
2. Her KARMAŞIK araç için referans karar test'i geçti
3. Vergi dilim + Ceza takvim + Miras dağıtım altyapıları kuruldu
4. Sitemap 43 araç URL içerir (otomatik)
5. Tüm Dalga A/B/C alt adımları ✅
6. README + roadmap + tech-debt güncel
7. `phase-7-complete` annotated tag

---

## §9 Faz 8 Önizleme

Faz 7 sonunda lansman kararı:
- Production deploy + ilk kullanıcılar
- Geri bildirim toplama
- Faz 8 teması veri-temelli (Adım 8.1 charter)

Faz 8+ aday başlıklar (Faz 6 §9'dan devren):
- Mobile/PWA (büyük tema)
- Multi-instance scaling (Redis backplane)
- Admin analytics dashboard
- D-I kategorileri için **otomatik veri çekme** (TÜİK, TCMB, Hazine, Resmi Gazete)
- #37 multi-tab tam çözüm
- #41 envanter denetim süreç borcu (yeni Faz başlangıcı her zaman)
- #42 referans belge merge süreci
- #43 enum comment drift düzeltme

---

## §10 Tech-debt Güncelleme (Adım 7.1 ile eklendi)

- **#42 Referans belge dangling commit riski:** Adım 7.0'da fark edildi
  (teknik rapor main'e merge edilmemiş, GitHub remote'taydı ama lokal yoktu)
- **#43 Enum comment drift:** CalculatorCategory enum D-I kategoriler için
  "Faz 5" yorumu var, gerçek Faz 7. Adım 7.2'de düzeltilebilir.

---

*Charter sürümü 1.0 — 5 Haziran 2026. Faz 7 boyunca güncel kalır;
değişiklikler commit'lerle işlenir.*

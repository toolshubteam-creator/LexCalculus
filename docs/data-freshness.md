# Lex Calculus — Veri Tazelik Takip Listesi

> **Amaç:** Hesaplayıcılarda kullanılan dış kaynaklı verilerin (TÜFE, TCMB, RG yayınları) ne zaman güncellenmesi gerektiğini takip etmek.
>
> **Faz 3 hedefi:** Bu kontrolün admin panelinde otomatik widget olarak gösterilmesi ve cron job ile e-posta uyarısı.
>
> **Şu an (Faz 2):** Manuel takip için bu dosyaya bakılır.

---

## AYLIK GÜNCELLEME

### TÜFE 12 Aylık Ortalama (Kira Artışı)
- **ToolSlug:** `tufe-12-ay-ort`
- **Hesaplayıcı:** Kira Artış Tespiti (`/hesapla/faiz/kira-artisi`)
- **Kaynak:** TÜİK — https://data.tuik.gov.tr (her ayın 3'ünde, saat 10:00)
- **Yedek:** Legalbank, ASMMMO, Alomaliye, hesaplama.net
- **Format:** Key=`YYYY-MM`, Value=yüzde (örn. `2026-03` → `32.82`)
- **Sistemdeki son veri:** Mart 2026
- **Sıradaki kontrol:** Her ayın 3'ü

---

## YARI YIL / DÖNEMSEL GÜNCELLEME

### TCMB Avans Oranı (Ticari Temerrüt Faizi)
- **ToolSlug:** `tcmb-avans`
- **Hesaplayıcı:** Ticari Temerrüt Faizi (`/hesapla/faiz/ticari-temerrut-faizi`)
- **Kaynak:** TCMB Reeskont ve Avans Faiz Oranları
- **Güncelleme:** Genelde 31 Aralık ve 30 Haziran civarı
- **3095 m.2 algoritması:** İlk 6 ay = önceki yıl 31 Aralık oranı; ikinci 6 ay = 30 Haziran oranı (≥5pp farklıysa)
- **Sıradaki kontrol:** Ocak ilk haftası, Temmuz ilk haftası

---

## OLAY BAZLI GÜNCELLEME

### Yasal Faiz Oranı (3095 m.1)
- **ToolSlug:** `yasal-faiz`
- **Hesaplayıcı:** Yasal Faiz, Akdî Temerrüt (alt sınır)
- **Kaynak:** BKK / Cumhurbaşkanı Kararı, Resmi Gazete
- **Tarihçe:** 01.01.2006 → %9 (BKK 2005/9831), 01.06.2024 → %24 (CBK 8485)
- **Kontrol:** Yeni BKK/CBK çıktığında

### Asgari Ücret
- **ToolSlug:** `asgari-ucret-brut`
- **Kaynak:** Asgari Ücret Tespit Komisyonu, RG
- **Güncelleme:** Aralık-Ocak (yıllık), bazen Haziran-Temmuz (ara zam)

### Kıdem Tazminatı Tavanı
- **ToolSlug:** `kidem-tazminati-tavan`
- **Kaynak:** Hazine ve Maliye Bakanlığı genelgesi
- **Güncelleme:** Ocak ve Temmuz başı

---

## YAKLAŞAN ÖNEMLİ TARİHLER

### AYM K.2025/164 (22.07.2025)
- **Etki:** 3095 s.K. m.1 — sözleşmeden kaynaklanmayan borç ilişkileri için iptal
- **Yürürlük:** 01.08.2026
- **Eylem:** O tarihte yasal düzenleme olmazsa Yasal Faiz hesaplayıcısının davranışı gözden geçirilecek
- **Şu an:** Hesaplayıcılar 01.08.2026 sonrası tarihler için sarı uyarı gösterir

---

## ÖZET — Önümüzdeki 12 Ay

| Tarih | Kontrol | Aksiyon |
|---|---|---|
| Her ayın 3'ü | TÜFE 12 ay ort. | Yeni satır ekle |
| 30 Haziran 2026 | TCMB avans yarı yıl | Değişiklik var mı |
| 1 Ağustos 2026 | AYM K.2025/164 yürürlük | Yasal Faiz davranışı |
| 31 Aralık 2026 | TCMB avans yıl sonu | Yeni yıl seed |
| Aralık 2026 - Ocak 2027 | Asgari ücret | Yeni satır |

---

**Son güncelleme:** Adım 2.20 (Kira Artışı) tamamlandığında.
**Sorumlu:** Sistem Yöneticisi (Esat).

# Lex Calculus — Faz 3 Yol Haritası

**Versiyon:** Faz 2 sonunda yazıldı (Adım 2.23, 27 Nisan 2026)
**Hedef Faz 3 başlangıcı:** Mayıs 2026
**Tahmini Faz 3 süresi:** 6-8 hafta

Bu doküman Faz 3'te yapılacak işlerin **kesin** ve **opsiyonel** ayrımını netleştirir.
Faz 3 kapsamı, Faz 2'de bilinçli olarak ertelenen feature'ları içerir.

---

## 1. Faz 3 Hedefleri

Faz 2 sonunda 17 hesaplayıcı çalışıyor ama platform **tek kullanıcı** ortamı —
yani admin@lexcalculus.local + birkaç test kullanıcısı. Faz 3 platformu
**çoklu kullanıcı + organizasyonel** ortama taşır.

Üç ana eksen:

**1. Veri Yönetimi** — Admin paneli ile parametre güncelleme
**2. Kullanıcı Deneyimi** — Hesap geçmişi UI, kullanıcı yönetimi
**3. Multi-tenant** — Organizasyon kavramı (hukuk büroları)

---

## 2. Adım Listesi (Önerilen Sıra)

### Adım 3.1 — Admin Layout & Authorization Policy

- /admin altında ayrı layout
- AdminOnly policy + role check (Admin, Editor)
- Sidebar navigation (Parametre, Kullanıcılar, Sistem Sağlığı, Veri Tazelik)
- Admin breadcrumb sistemi

**Süre:** 2-3 gün

### Adım 3.2 — FormulaParameter CRUD

- /admin/parameters → liste (filter: ToolSlug, Key, EffectiveDate)
- Yeni satır ekle / mevcut düzenle / soft-delete
- ParameterChangeLog (her değişiklikte eski/yeni değer + kim/ne zaman)
- Validation: ToolSlug + Key + EffectiveDate unique constraint
- Bulk import (CSV upload — TCMB/TÜFE seed yenileme)

**Süre:** 4-5 gün

### Adım 3.3 — Veri Tazelik Widget

- Admin dashboard'da: hangi parametre güncel mi?
- ExpectedUpdateFrequency'ye göre uyarı:
  * Daily/Weekly/Monthly/Quarterly/Biannual/Annual
  * LastUpdatedDate + Frequency = beklenen son tarih
  * Geçmişse kırmızı uyarı, yaklaşıyorsa sarı
- Widget her parametre kategorisi için (TÜFE, TCMB, Kıdem tavanı, asgari ücret)

**Süre:** 2-3 gün

### Adım 3.4 — Kullanıcı Hesap Geçmişi UI

- /hesap-gecmisim → kullanıcının tüm hesapları
- Filter: tool, tarih aralığı, etiket
- Detay görüntüleme (InputJson + OutputJson de-serialize, eski hesabı geri görmek)
- "Bu hesabı düzenle" → form'u eski input'larla doldur, yeniden hesapla
- Etiket ekleme (UserLabel) + dava referansı (CaseReference)
- Export (PDF, Excel) — tek hesap veya filtrelenen liste

**Süre:** 5-7 gün

### Adım 3.5 — LifeTable CRUD (Aktüerya)

- /admin/life-tables → TRH 2010 ve gelecek tabloları yönetimi
- Yeni LifeTable versiyon ekleme (örn. TRH 2025 çıkarsa)
- LifeTableRow editor (yaş-cinsiyet bazında manuel veya CSV upload)
- Calculator hangi LifeTable'ı kullanıyor seçim (default + override)

**Süre:** 3-4 gün

### Adım 3.6 — Kullanıcı Yönetimi

- /admin/users → liste (filter: role, status, kayıt tarihi)
- Detay: profil bilgileri, hesap geçmişi sayısı, son aktivite
- Rol atama (Admin / Editor / Premium / Free)
- Hesap askıya alma / kapama
- Şifre sıfırlama bağlantısı gönderme

**Süre:** 3-4 gün

### Adım 3.7 — Organization Multi-Tenant Altyapı

- Organizations tablosu (hukuk bürosu, baro, vs.)
- UserOrganizations many-to-many (kullanıcı birden çok org'a üye olabilir)
- CalculationHistory.OrganizationId nullable FK ekle
- Org admin'i org içi tüm hesapları görebilir
- Org bazında kullanım istatistiği

**Süre:** 5-7 gün

### Adım 3.8 — Aktivite Logu UI

- ActivityLogs tablosu (Faz 1'de oluşturuldu, kullanılmıyor)
- Admin panelinde tüm parametre değişiklikleri, kullanıcı login/logout, yetki değişiklikleri
- Filter: kullanıcı, tarih, eylem türü
- CSV export

**Süre:** 2-3 gün

### Adım 3.9 — Faz 3 Final

- README güncellemesi
- Phase 4 roadmap
- Tag: phase-3-complete

**Süre:** 1 gün

**Toplam Faz 3:** ~30-40 iş günü (~6-8 hafta)

---

## 3. Faz 3 Dışında Bırakılan İşler

Aşağıdaki feature'lar Faz 3'te YAPILMAYACAK. Hangisinin Faz 4 olduğu, hangisinin Faz 5 olduğu listede.

### Faz 4'e Ertelenenler (Sosyal Platform)

- **Kullanıcı plan sistemi** (free/pro/enterprise) — Stripe entegrasyonu, abone yönetimi
- **Mesleki bağlantılar** — kullanıcılar arası bağlantı isteği/kabul
- **SignalR mesajlaşma** — real-time mesajlaşma altyapısı
- **Bildirim sistemi** — yeni mesaj/bağlantı/yorum
- **Üye profil sayfaları** — kamuya açık profiller (Person schema)

### Faz 5'e Ertelenenler (Otomatik Veri & Diğer Modüller)

- **TÜİK API entegrasyonu** — TÜFE otomatik güncelleme
- **TCMB API** — avans faizi otomatik güncelleme
- **Hazine sayfası scraping** — kıdem tavanı otomatik
- **Resmi Gazete RSS** — mevzuat değişiklik takibi
- **D-I kategorileri kalan calculator'lar** (Gayrimenkul, Aile/Miras, Ceza, Vergi, Ticaret, Bilirkişi)
- **Hangfire job altyapısı** — duyuru çekme, e-posta bildirim, veri yenileme cron'ları

### Belirsiz / Karar Bekleyen

- **CalculationHistory archiving** — eski hesapları cold storage'a taşıma stratejisi
- **GDPR/KVKK uyumu** — açık rıza onayı, veri silme hakkı, veri işleme envanteri
- **Performans iyileştirme** — Redis cache, query optimization, Core Web Vitals

---

## 4. Faz 3 İçin Hazır Olan Altyapı

Faz 2'de bilinçli olarak Faz 3 hazırlığı yapılan yerler:

| Hazırlık | Faz | Açıklama |
|---|---|---|
| FormulaParameter.ExpectedUpdateFrequency | 2.20 | Veri tazelik widget'ı için |
| FormulaParameter.LastUpdatedDate | 2.20 | Veri tazelik widget'ı için |
| FormulaParameter.Notes | 2.20 | Admin panelinde tooltip için |
| CalculationHistory.UserLabel | 2.22 | Kullanıcı dava etiketi |
| CalculationHistory.CaseReference | 2.22 | Dava dosya numarası |
| CalculationHistory indexler | 2.22 | (UserId), (UserId+CreatedAt), (UserId+ToolSlug) |
| CalculationHistory soft-delete | 2.22 | Kullanıcı sildiklerini geri alabilsin |
| ApplicationUser : IdentityUser&lt;int&gt; | 1 | Multi-tenant için int FK uygun |

Faz 3 başlarken bu altyapı yeniden yazılmayacak, üzerine ekleme yapılacak.

---

## 5. Faz 3 Riskleri

**Risk 1: Multi-tenant performans**
- Her query'ye OrganizationId filtresi eklemek zorunlu
- Yanlış yazılırsa kullanıcı başka org'un verisini görür (data leak)
- Mitigasyon: Global query filter (EF Core), her test'te org isolation kontrolü

**Risk 2: Admin parametre güncellemesi sırasında concurrency**
- İki admin aynı parametreyi aynı anda düzenlerse last-write-wins kaybedebilir
- Mitigasyon: RowVersion / optimistic concurrency

**Risk 3: Hesap geçmişi PDF export performansı**
- Eski kullanıcı 1000+ hesap birikirse PDF export yavaş
- Mitigasyon: Async generation + email link, sayfalama

**Risk 4: Bulk CSV import validation**
- TÜFE/TCMB toplu yükleme yanlış format'ta gelirse seed bozulabilir
- Mitigasyon: Strict CSV schema, dry-run önizleme, transaction rollback

---

**Doküman versiyonu:** 1.0 (27.04.2026, Adım 2.23)
**Bakım sorumlusu:** Sistem Yöneticisi (toolshubteam@gmail.com)
**Sonraki güncelleme:** Faz 3 başında her alt adım tamamlandıkça revize edilecek.

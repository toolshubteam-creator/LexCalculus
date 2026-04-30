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

### Adım 3.3 — Veri Tazelik Bildirim Sistemi

**Amaç:** Avukatlar platforma sürekli girmiyor (dava bazlı kullanım).
Bir veri eskidiğinde, admin'in admin panele girmesini beklemeden
proaktif uyarı sistemi gerekiyor. Yoksa eski parametreyle hesap
yapılır, mahkemede sorun çıkar.

**Kapsam:**

1. **Hangfire altyapı kurulumu**
   - LexCalculus.Jobs projesi zaten var (Faz 1), Hangfire bağlanmadı
   - Hangfire DB tabloları (otomatik migration)
   - Hangfire dashboard /admin/hangfire (AdminOnly)

2. **E-posta gönderim servisi**
   - IEmailService (SendGrid veya SMTP)
   - Template sistemi (HTML + plaintext fallback)
   - Faz 4'te bildirim/şifre sıfırlama da bu servisi kullanır

3. **Notifications tablosu**
   - Kullanıcı bildirim merkezi (Faz 4 sosyal platform da bunu kullanır)
   - Type: DataFreshness, ParameterChange, SystemAlert (gelecek için Connection, Message)
   - IsRead, CreatedAt, ActionUrl

4. **Veri tazelik kontrol logic**
   - DataFreshnessCheckJob (Hangfire recurring, günde 1 kez)
   - Her FormulaParameter için: LastUpdatedDate + ExpectedUpdateFrequency = beklenen son tarih
   - Geçmişse → admin'lere e-posta + Notification kaydı
   - Yaklaşıyorsa (Frequency / 4 kala) → uyarı niteliğinde Notification

5. **Admin Dashboard Widget**
   - "Veri Sağlığı" kartı
   - Kategori bazında durum: Yeşil (güncel), Sarı (yaklaşıyor), Kırmızı (geçti)
   - Tıklayınca /admin/parameters?status=stale filtresine gider

6. **Bildirim merkezi UI**
   - Kullanıcı header'ında çan ikonu + okunmamış sayısı
   - /bildirimlerim → liste, filtreleme, okundu işaretleme

**Bağımlılıklar:**
- Adım 3.1 (Admin Layout) tamamlanmış olmalı
- Adım 3.2 (FormulaParameter CRUD) tamamlanmış olmalı (LastUpdatedDate güncellenince
  cron'un yeniden hesaplaması gerekir)

**Süre:** 5-6 gün

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

**Status:** ✅ Tamamlandı (2026-04-30)
**Commit range:** `e1c5429..a98a7fb`

> **Vizyon notu (29 Nisan 2026):** Sistem ücretsiz, hedef kitle tüm
> vatandaşlar. Plan kavramı (Free/Pro/Enterprise) yok. Multi-tenant
> özelliği hukuk bürosu ekipleri (5 avukat ortak çalışma) senaryosu
> için kalıcı; bireysel kullanıcılar TenantId nullable pattern ile
> etkilenmez. Detaylı yol haritası docs/'a Adım 3.9'da kalıcı
> eklenecek.

- Organizations tablosu (hukuk bürosu, baro, vs.)
- UserOrganizations many-to-many (kullanıcı birden çok org'a üye olabilir)
- CalculationHistory.OrganizationId nullable FK ekle
- Org admin'i org içi tüm hesapları görebilir
- Org bazında kullanım istatistiği

**Süre:** 5-7 gün

#### Adım 3.7 — Kapanış Özeti (2026-04-30)

5 parçada tamamlandı (P1, P2a, P2b, P3, P4).

**Commit zinciri:**
- P1/5: `3737bdb` — Tenant entity + global query filter + AsAdminQuery
- P2a/5: `810eaa4` + `77d611a` hotfix — Tenant CRUD admin + SlugHelper + owner protection
- P2b/5: `fc17324` + `26673cc` hotfix — TenantRequest akışı + SiteUrl SeoSettings
- P3/5: `e0a9d31` — Davet sistemi + register integration
- P4/5: `a98a7fb` — Tenant context UI + paylaşım toggle + scope filter

**Metrikler:**
- Test: 326 → 375 (+49)
- LOC: ~5000 yeni
- Migration: 4 (AddTenantFoundation, AddTenantRequest, AddTenantInvitation, snapshot updates)
- Regresyon: 0

**Mimari kararlar:**
- Karar 1: Tek TenantId alanı (M2M değil)
- Karar 2: Hesap geçmişi varsayılan özel, opt-in paylaşım
- Karar 3: Tenant içi sadece owner + üye (granüler rol yok)
- Karar 6: Paylaşılan hesap üye ayrılınca/tenant silinince TenantId korunur
- ShareWithTenant Core/Input DTO'larına SIZDIRILMADI (controller seviyesi)

**Vizyon uyumu:** Plan field eklenmedi. Bireysel vatandaş kullanıcılar tenant UI hiç görmez.

**Açık tech-debt (Adım 3.9'a):**
- Madde 8: P3/5 TenantYonetim/Index.cshtml inline style ihlali

**Sonraki:** Adım 3.8 — Activity Log (audit trail) + expired davet/talep cleanup.

### Adım 3.8 — Aktivite Logu UI

**Status:** ✅ Tamamlandı (2026-04-30)
**Commit range:** `15a9f5e..44b0145`

- ActivityLogs tablosu (Faz 1'de oluşturuldu, kullanılmıyor)
- Admin panelinde tüm parametre değişiklikleri, kullanıcı login/logout, yetki değişiklikleri
- Filter: kullanıcı, tarih, eylem türü
- CSV export

**Süre:** 2-3 gün

#### Adım 3.8 — Kapanış Özeti (2026-04-30)

2 parçada tamamlandı (P1, P2).

**Commit zinciri:**
- P1/2: `537877c` — ActivityLog entity + service + 16 entegrasyon + admin UI
- P2/2: `44b0145` — ExpireInvitationsJob (Hangfire, günde 1 kez 03:00 Europe/Istanbul)

**Metrikler:**
- Test: 375 → 391 (+16)
- Migration: 1 (AddActivityLog)
- Hangfire recurring jobs: 1 yeni (expire-invitations)
- Regresyon: 0

**Mimari kararlar:**
- Karar 1: Logging kapsamı orta — admin işlemleri + hassas değişiklikler (geniş değil)
- Karar 3: Service-level manuel log (interceptor değil)
- Karar 5: ActivityLog kendi audit'i yok (sonsuz döngü riski)
- Karar 7: TenantRequest expiry yok (sadece davet expire edilir)
- Karar 9: Multi-tenant query filter yok (admin only feature)
- HttpContext null'da defansif (background job'larda crash yok)

**Entegrasyon noktaları (16 toplam):**
- UserAdmin: 4 (SetActive aktif/pasif, ChangeRole, SendPasswordReset)
- TenantAdmin: 5 (Create, Update, SoftDelete, AddMember, RemoveMember)
- TenantRequest: 3 (Approve, Reject, Cancel)
- TenantInvitation: 4 (Create, Cancel, Accept, AcceptForNewUser)
- FormulaParameter: 3 (Create, Update, Delete)
- LifeTable: 3 (Create, Activate, UpdateRow)

**Açık tech-debt:**
- Madde 9: ActivityLog retention policy (KVKK) — avukat görüşü gerekiyor

**Sonraki:** Adım 3.9 — Faz 3 final cleanup + tag (phase-3-complete).

### Adım 3.9 — Faz 3 Final

- README güncellemesi
- Phase 4 roadmap
- Tag: phase-3-complete

**Süre:** 1 gün

**Toplam Faz 3:** ~35-45 iş günü (~7-9 hafta)

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

**Not:** Hangfire altyapısı ve e-posta bildirim sistemi Faz 3.3'e çekildi
(veri tazelik bildirim sisteminin temel bileşenleri). Faz 5'teki
"otomatik veri çekme" işleri bu altyapı üzerine inşa edilecek.

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
| LexCalculus.Jobs projesi | 1 | Hangfire için iskelet hazır, paket eklenmesi yeterli |
| ApplicationUser email + emailconfirmed | 1 | Bildirim e-postası gönderilebilir |

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

**Risk 5: E-posta gönderim güvenilirliği**
- SendGrid/Mailgun outage'ında veri tazelik uyarısı kaybolur
- Bounce / spam folder durumları yönetilmeli
- Mitigasyon: Gönderim retry policy (Hangfire built-in), bildirim merkezinde
  in-app fallback (kullanıcı admin'e girince yine görür)

---

**Doküman versiyonu:** 1.0 (27.04.2026, Adım 2.23)
**Bakım sorumlusu:** Sistem Yöneticisi (toolshubteam@gmail.com)
**Sonraki güncelleme:** Faz 3 başında her alt adım tamamlandıkça revize edilecek.

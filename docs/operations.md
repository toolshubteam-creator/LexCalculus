# Lex Calculus — Operasyon Notları

Geliştiricinin ve admin'in bilmesi gereken işletim detayları.

## Cache invalidation

Sistem, formul parametrelerini Redis (veya MemoryDistributedCache fallback)
üzerinde cache'ler. Cache temizleme **servis katmanında** olur:

- `IFormulaParameterService.AddAsync`, `UpdateAsync`, `SoftDeleteAsync`
  otomatik olarak `InvalidateAsync(toolSlug, key)` çağırır.
- Admin paneli (`/admin/parametreler`) bu metotları kullanır.

### Tehlike: manuel DB müdahalesi

Doğrudan SQL ile FormulaParameters tablosuna yazma/silme **cache invalidation
tetiklemez**. Bu durumda calculator eski değeri kullanmaya devam eder.

**Çözüm:**
- Tercih edilen: admin panelini kullan (`/admin/parametreler`).
- Acil durum (manuel SQL gerekiyorsa):
  ```
  redis-cli FLUSHDB                    # Redis kullanıyorsanız
  veya
  app restart                          # MemoryDistributedCache fallback'inde
  ```

## Hangfire dashboard

`/admin/hangfire` — sadece Admin rolü. Recurring job'lar:

- `data-freshness-check` — her gün **06:00 (Europe/Istanbul)**. Stale
  parametreleri tarar, admin'e digest bildirim + e-posta gönderir.
  - **Stale tespiti:** Her (slug, key) çiftinin **en yeni effective-dated**
    satırı kontrol edilir. Eski versiyonlar bypass.
  - **Dedup:** 7 günlük pencerede aynı admin'e aynı digest tekrar
    gönderilmez (`NotificationService.CreateAsync` dedup'ıyla).
  - **E-posta opt-out:** `ApplicationUser.NotificationsEmailEnabled = false`
    ise sadece in-app notification kalır, e-posta atlanır.

## E-posta provider

`appsettings.json:Email:Provider` ile seçilir:
- `Logging` — dev varsayılan; gerçek mail gönderilmez, `Logs/log-*.txt`'ye yazılır.
- `Smtp` — System.Net.Mail; MailHog (port 1025) gibi self-hosted için.
- `SendGrid` — production; API key User Secrets'te tanımlı olmalı.

Test gönderimi: `/admin/email/test` (admin only).

## Manuel job tetikleme

Hangfire dashboard `/admin/hangfire` → Recurring Jobs sekmesi → ilgili job
yanındaki "Trigger now". Tetiklenen job'lar Console + Logs'a yazar; Notification
oluşturur (admin'lere); opt-in admin'lere e-posta atar.

---

**Son güncelleme:** Faz 3.3 Parça 4a (Adım 3.3.4 başlangıcı).

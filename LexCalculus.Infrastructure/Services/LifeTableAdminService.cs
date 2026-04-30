using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

public sealed class LifeTableAdminService : ILifeTableAdminService
{
    private readonly ApplicationDbContext _ctx;
    private readonly ILifeTableService _calculatorSvc;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<LifeTableAdminService> _logger;

    public LifeTableAdminService(
        ApplicationDbContext ctx,
        ILifeTableService calculatorSvc,
        IActivityLogService activityLog,
        ILogger<LifeTableAdminService> logger)
    {
        _ctx = ctx;
        _calculatorSvc = calculatorSvc;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LifeTable>> GetAllAsync(CancellationToken ct = default)
    {
        return await _ctx.Set<LifeTable>()
            .OrderByDescending(t => t.IsActive)
            .ThenByDescending(t => t.EffectiveDate)
            .ToListAsync(ct);
    }

    public async Task<LifeTable?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _ctx.Set<LifeTable>()
            .Include(t => t.Rows)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task ActivateAsync(int id, CancellationToken ct = default)
    {
        var target = await _ctx.Set<LifeTable>().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException($"LifeTable {id} bulunamadı.");

        if (target.IsActive)
        {
            _logger.LogInformation("LifeTable {Id} zaten aktif; aktivasyon atlandı.", id);
            return;
        }

        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
        try
        {
            var currentlyActive = await _ctx.Set<LifeTable>()
                .Where(t => t.IsActive)
                .ToListAsync(ct);

            foreach (var t in currentlyActive)
                t.IsActive = false;

            target.IsActive = true;

            await _ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "LifeTable aktivasyonu: {OldCount} eski aktif → {NewId} ({NewCode}) yeni aktif.",
                currentlyActive.Count, target.Id, target.Code);

            await TryInvalidateCacheAsync(ct);

            await _activityLog.LogAsync(
                action: "LifeTable.Activate",
                entityType: nameof(LifeTable),
                entityId: target.Id,
                description: $"LifeTable aktif yapıldı: {target.Code} - {target.Name}",
                metadata: new { target.Code, DeactivatedCount = currentlyActive.Count },
                ct: ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeactivateActiveAsync(CancellationToken ct = default)
    {
        var active = await _ctx.Set<LifeTable>()
            .Where(t => t.IsActive)
            .ToListAsync(ct);

        if (active.Count == 0)
        {
            _logger.LogInformation("Aktif tablo yok; deaktivasyon atlandı.");
            return;
        }

        foreach (var t in active)
            t.IsActive = false;

        await _ctx.SaveChangesAsync(ct);
        await TryInvalidateCacheAsync(ct);

        _logger.LogInformation("{Count} aktif tablo pasif yapıldı.", active.Count);
    }

    public async Task<int> CreateAsync(
        string code, string name, DateTime effectiveDate,
        string? source, string? note,
        IReadOnlyList<LifeTableRow> rows,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code zorunlu.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name zorunlu.", nameof(name));
        if (rows.Count != 200)
            throw new ArgumentException($"200 satır bekleniyor, {rows.Count} verildi.", nameof(rows));

        var trimmedCode = code.Trim();

        var exists = await _ctx.Set<LifeTable>()
            .AnyAsync(t => t.Code == trimmedCode, ct);
        if (exists)
            throw new InvalidOperationException($"Bu kod zaten kullanılıyor: '{trimmedCode}'.");

        var entity = new LifeTable
        {
            Code = trimmedCode,
            Name = name.Trim(),
            EffectiveDate = effectiveDate,
            IsActive = false,
            Source = source?.Trim(),
            Note = note?.Trim(),
            Rows = rows.Select(r => new LifeTableRow
            {
                Yas = r.Yas,
                Cinsiyet = r.Cinsiyet,
                BekledigiYasam = r.BekledigiYasam
            }).ToList()
        };

        _ctx.Set<LifeTable>().Add(entity);
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "LifeTable.Create",
            entityType: nameof(LifeTable),
            entityId: entity.Id,
            description: $"LifeTable oluşturuldu: {entity.Code} - {entity.Name} ({rows.Count} satır)",
            metadata: new { entity.Code, entity.Name, EffectiveDate = entity.EffectiveDate, RowCount = rows.Count },
            ct: ct);

        _logger.LogInformation(
            "LifeTable oluşturuldu: Id={Id} Code={Code} Rows={RowCount}",
            entity.Id, entity.Code, rows.Count);

        return entity.Id;
    }

    public async Task UpdateRowAsync(int rowId, decimal newValue, CancellationToken ct = default)
    {
        if (newValue <= 0)
            throw new ArgumentException("BekledigiYasam pozitif olmalı.", nameof(newValue));

        var row = await _ctx.Set<LifeTableRow>()
            .Include(r => r.LifeTable)
            .FirstOrDefaultAsync(r => r.Id == rowId, ct)
            ?? throw new InvalidOperationException($"LifeTableRow {rowId} bulunamadı.");

        var oldValue = row.BekledigiYasam;
        row.BekledigiYasam = newValue;

        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "LifeTable.UpdateRow",
            entityType: nameof(LifeTableRow),
            entityId: rowId,
            description: $"LifeTable satırı güncellendi: {row.LifeTable!.Code} Yaş={row.Yas} Cinsiyet={row.Cinsiyet}",
            metadata: new
            {
                TableCode = row.LifeTable!.Code,
                row.Yas,
                Cinsiyet = row.Cinsiyet.ToString(),
                OldValue = oldValue,
                NewValue = newValue
            },
            ct: ct);

        _logger.LogInformation(
            "LifeTableRow güncellendi: Id={Id} TableCode={Code} Yas={Yas} Cinsiyet={Cinsiyet} {Old} → {New}",
            rowId, row.LifeTable!.Code, row.Yas, row.Cinsiyet, oldValue, newValue);

        if (row.LifeTable!.IsActive)
            await TryInvalidateCacheAsync(ct);
    }

    private async Task TryInvalidateCacheAsync(CancellationToken ct)
    {
        try
        {
            await _calculatorSvc.InvalidateCacheAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache invalidation hatası — devam ediliyor (cache TTL ile yenilenir).");
        }
    }
}

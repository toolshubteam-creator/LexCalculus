using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class TenantAdminService : ITenantAdminService
{
    private readonly ApplicationDbContext _ctx;

    public TenantAdminService(ApplicationDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<List<TenantListItemDto>> GetAllAsync(
        bool includeDeleted, string? search, CancellationToken ct = default)
    {
        // Admin sorgusu — query filter bypass.
        var q = _ctx.Tenants.AsAdminQuery().AsQueryable();

        if (!includeDeleted)
            q = q.Where(t => !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(t => t.Name.Contains(term) || t.Slug.Contains(term));
        }

        var rows = await q
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                t.CreatedAt,
                t.IsDeleted,
                OwnerEmail = t.Owner != null ? t.Owner.Email : null,
                OwnerUserName = t.Owner != null ? t.Owner.UserName : null,
                MemberCount = _ctx.Users.AsAdminQuery().Count(u => u.TenantId == t.Id)
            })
            .ToListAsync(ct);

        return rows.Select(r => new TenantListItemDto(
            r.Id, r.Name, r.Slug,
            OwnerUserName: r.OwnerEmail ?? r.OwnerUserName ?? "(silinmiş)",
            r.MemberCount, r.CreatedAt, r.IsDeleted)).ToList();
    }

    public async Task<TenantDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var tenant = await _ctx.Tenants
            .AsAdminQuery()
            .Include(t => t.Owner)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant == null) return null;

        var members = await _ctx.Users
            .AsAdminQuery()
            .Where(u => u.TenantId == id)
            .OrderBy(u => u.Email)
            .Select(u => new UserOptionDto(u.Id, u.UserName ?? "", u.Email ?? ""))
            .ToListAsync(ct);

        return new TenantDetailDto(
            tenant.Id, tenant.Name, tenant.Slug,
            tenant.OwnerUserId,
            tenant.Owner?.Email ?? tenant.Owner?.UserName ?? "(silinmiş)",
            tenant.CreatedAt, tenant.IsDeleted,
            members);
    }

    public async Task<int> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Tenant adı zorunlu.");

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugHelper.Generate(request.Name)
            : SlugHelper.Generate(request.Slug);

        if (string.IsNullOrEmpty(slug))
            throw new InvalidOperationException("Slug üretilemedi (geçerli karakter yok).");

        var slugTaken = await _ctx.Tenants
            .AsAdminQuery()
            .AnyAsync(t => t.Slug == slug, ct);
        if (slugTaken)
            throw new InvalidOperationException("Slug already in use");

        var owner = await _ctx.Users
            .AsAdminQuery()
            .FirstOrDefaultAsync(u => u.Id == request.OwnerUserId, ct)
            ?? throw new InvalidOperationException("Owner kullanıcı bulunamadı.");

        if (owner.TenantId.HasValue)
            throw new InvalidOperationException("User already belongs to a tenant");

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            OwnerUserId = request.OwnerUserId,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.Tenants.Add(tenant);
        await _ctx.SaveChangesAsync(ct);

        owner.TenantId = tenant.Id;
        await _ctx.SaveChangesAsync(ct);

        return tenant.Id;
    }

    public async Task UpdateAsync(int id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await _ctx.Tenants
            .AsAdminQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Tenant bulunamadı.");

        if (tenant.IsDeleted)
            throw new InvalidOperationException("Silinmiş tenant düzenlenemez.");

        var newSlug = SlugHelper.Generate(request.Slug);
        if (string.IsNullOrEmpty(newSlug))
            throw new InvalidOperationException("Slug geçersiz.");

        if (newSlug != tenant.Slug)
        {
            var slugTaken = await _ctx.Tenants
                .AsAdminQuery()
                .AnyAsync(t => t.Slug == newSlug && t.Id != id, ct);
            if (slugTaken)
                throw new InvalidOperationException("Slug already in use");
        }

        tenant.Name = request.Name.Trim();
        tenant.Slug = newSlug;

        if (tenant.OwnerUserId != request.OwnerUserId)
        {
            var newOwner = await _ctx.Users
                .AsAdminQuery()
                .FirstOrDefaultAsync(u => u.Id == request.OwnerUserId, ct)
                ?? throw new InvalidOperationException("Yeni owner kullanıcı bulunamadı.");

            if (newOwner.TenantId.HasValue && newOwner.TenantId != id)
                throw new InvalidOperationException("Yeni owner başka bir tenant'a üye.");

            tenant.OwnerUserId = newOwner.Id;
            if (newOwner.TenantId != id)
                newOwner.TenantId = id;
        }

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        var tenant = await _ctx.Tenants
            .AsAdminQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Tenant bulunamadı.");

        if (tenant.IsDeleted) return;

        tenant.IsDeleted = true;
        tenant.DeletedAt = DateTime.UtcNow;

        // Üyelerin TenantId'sini elle null yap (cascade SetNull migration tarafında zaten var,
        // ama soft-delete'te FK trigger çalışmaz — manuel sıfırlama).
        var members = await _ctx.Users
            .AsAdminQuery()
            .Where(u => u.TenantId == id)
            .ToListAsync(ct);
        foreach (var m in members)
            m.TenantId = null;

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task AddMemberAsync(int tenantId, int userId, CancellationToken ct = default)
    {
        var tenant = await _ctx.Tenants
            .AsAdminQuery()
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant bulunamadı veya silinmiş.");

        var user = await _ctx.Users
            .AsAdminQuery()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        if (user.TenantId.HasValue)
            throw new InvalidOperationException("User already belongs to a tenant");

        user.TenantId = tenantId;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(int tenantId, int userId, CancellationToken ct = default)
    {
        var tenant = await _ctx.Tenants
            .AsAdminQuery()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Tenant bulunamadı.");

        if (tenant.OwnerUserId == userId)
            throw new InvalidOperationException("Owner çıkarılamaz. Önce owner değiştirin.");

        var user = await _ctx.Users
            .AsAdminQuery()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Kullanıcı bu tenant'ın üyesi değil.");

        user.TenantId = null;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<List<UserOptionDto>> GetAvailableUsersAsync(CancellationToken ct = default)
    {
        return await _ctx.Users
            .AsAdminQuery()
            .Where(u => u.TenantId == null)
            .OrderBy(u => u.Email)
            .Select(u => new UserOptionDto(u.Id, u.UserName ?? "", u.Email ?? ""))
            .ToListAsync(ct);
    }
}

using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexCalculus.Tests.TestHelpers;

public static class TestDbContextFactory
{
    /// <summary>
    /// Default — NoOp tenant context (CurrentUserId=null, CurrentTenantId=null).
    /// Tenant filter etkisinden bağımsız test'ler için.
    /// </summary>
    public static ApplicationDbContext Create(
        string? databaseName = null,
        ITenantContext? tenantContext = null)
    {
        var options = CreateOptions(databaseName);
        tenantContext ??= new TestTenantContext();
        return new ApplicationDbContext(options, tenantContext);
    }

    /// <summary>
    /// Convenience overload — testin perspektifini explicit kurar.
    /// CurrentTenantId=null, CurrentUserId=actAsUserId.
    /// </summary>
    public static ApplicationDbContext Create(int actAsUserId, string? databaseName = null)
        => Create(databaseName, new TestTenantContext { CurrentUserId = actAsUserId });

    /// <summary>
    /// Aynı in-memory DB'yi paylaşmak için DbContextOptions üretir.
    /// Tenant izolasyon test'leri seed phase ile assert phase arasında
    /// farklı tenant context kullanmak ister; bu overload paylaşımı sağlar.
    /// </summary>
    public static DbContextOptions<ApplicationDbContext> CreateOptions(string? databaseName = null)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .AddInterceptors(new AuditInterceptor())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    /// <summary>
    /// Pre-built options'la context yarat (shared in-memory DB için).
    /// </summary>
    public static ApplicationDbContext Create(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext? tenantContext = null)
    {
        tenantContext ??= new TestTenantContext();
        return new ApplicationDbContext(options, tenantContext);
    }
}

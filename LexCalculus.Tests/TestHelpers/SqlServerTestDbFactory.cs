using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Tests.TestHelpers;

/// <summary>
/// Adım 5.8 P2 (charter Karar 10) — SQL Server LocalDB test veritabanı.
///
/// InMemory <c>TestDbContextFactory</c>'nin SQL Server karşılığı. Statik
/// async factory pattern: her test sınıfı <see cref="IAsyncLifetime"/>
/// implement eder, <see cref="InitializeAsync"/>'te <see cref="CreateAsync"/>
/// çağırır → unique bir LocalDB veritabanı (<c>LexCalculusTest_{guid}</c>)
/// oluşturulur ve migration uygulanır; <see cref="DisposeAsync"/>'te
/// veritabanı düşürülür.
///
/// xUnit her <c>[Fact]</c> için test sınıfının yeni bir instance'ını
/// oluşturduğundan, bu pattern InMemory'nin "her <c>Create()</c> çağrısı
/// taze DB" davranışıyla birebir simetriktir — sadece per-test migration
/// maliyeti eklenir.
///
/// Metod imzaları kasıtlı olarak <c>TestDbContextFactory</c> ile birebir
/// aynıdır; geçişte çağrı yerleri yalnızca <c>TestDbContextFactory.</c> →
/// <c>_db.</c> olarak değişir.
/// </summary>
public sealed class SqlServerTestDb : IAsyncDisposable
{
    public string ConnectionString { get; }

    private SqlServerTestDb(string connectionString) => ConnectionString = connectionString;

    /// <summary>Unique bir LocalDB veritabanı oluşturur ve migration uygular.</summary>
    public static async Task<SqlServerTestDb> CreateAsync()
    {
        var databaseName = $"LexCalculusTest_{Guid.NewGuid():N}";
        var connectionString =
            $"Server=(localdb)\\mssqllocaldb;Database={databaseName};" +
            "Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=60";

        var db = new SqlServerTestDb(connectionString);

        await using var ctx = new ApplicationDbContext(db.BuildOptions(), new TestTenantContext());
        await ctx.Database.MigrateAsync();

        return db;
    }

    // ---------------------------------------------------------------------
    // TestDbContextFactory ile simetrik API — geçişte imzalar değişmesin diye.
    // databaseName parametresi SQL Server'da anlamsız (DB bu instance'a sabit);
    // imza uyumu için korunur, yok sayılır.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Default — NoOp tenant context (CurrentUserId=null, CurrentTenantId=null).
    /// </summary>
    public ApplicationDbContext Create(
        string? databaseName = null,
        ITenantContext? tenantContext = null)
        => new(BuildOptions(), tenantContext ?? new TestTenantContext());

    /// <summary>
    /// Convenience overload — testin perspektifini explicit kurar.
    /// CurrentTenantId=null, CurrentUserId=actAsUserId.
    /// </summary>
    public ApplicationDbContext Create(int actAsUserId, string? databaseName = null)
        => Create(databaseName, new TestTenantContext { CurrentUserId = actAsUserId });

    /// <summary>
    /// Pre-built options'la context yarat. SQL Server'da aynı fiziksel DB'yi
    /// paylaşmak için kullanılır (seed phase / assert phase farklı tenant
    /// context'leri).
    /// </summary>
    public ApplicationDbContext Create(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext? tenantContext = null)
        => new(options, tenantContext ?? new TestTenantContext());

    /// <summary>
    /// Bu test DB'sine bağlı <see cref="DbContextOptions"/> üretir — aynı DB'yi
    /// paylaşan farklı tenant context'leri için (InMemory <c>CreateOptions</c>
    /// karşılığı).
    /// </summary>
    public DbContextOptions<ApplicationDbContext> CreateOptions(string? databaseName = null)
        => BuildOptions();

    private DbContextOptions<ApplicationDbContext> BuildOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(new AuditInterceptor())
            .Options;

    /// <summary>
    /// Audit interceptor'sız context — seed sırasında özelleştirilmiş
    /// <c>CreatedAt</c> set etmek isteyen testler için (AuditInterceptor
    /// normalde bunu otomatik dolduruyor). Aynı fiziksel DB'ye bağlanır.
    /// </summary>
    public ApplicationDbContext CreateNoAuditContext(ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ApplicationDbContext(options, tenantContext ?? new TestTenantContext());
    }

    /// <summary>Connection pool'u boşaltıp veritabanını düşürür (DROP DATABASE).</summary>
    public async ValueTask DisposeAsync()
    {
        // Açık connection varken DROP "database in use" verir — pool'u boşalt.
        SqlConnection.ClearAllPools();

        await using var ctx = new ApplicationDbContext(BuildOptions(), new TestTenantContext());
        await ctx.Database.EnsureDeletedAsync();
    }
}

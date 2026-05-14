using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Tests.TestHelpers;

/// <summary>
/// Adım 5.8 (charter Karar 10) — SQL Server LocalDB test fixture.
///
/// InMemory provider'ın aksine gerçek SQL Server semantiği (IDENTITY,
/// collation, transaction, ExecuteUpdate/Delete, GroupBy translation)
/// ile çalışır. Her test SINIFI için unique bir LocalDB veritabanı
/// (<c>LexCalculusTest_{guid}</c>) oluşturulur; migration uygulanır;
/// sınıf bittiğinde veritabanı düşürülür.
///
/// Kullanım: <c>IClassFixture&lt;SqlServerTestFixture&gt;</c> — fixture
/// constructor injection ile alınır, <see cref="CreateContext()"/> her
/// çağrıda taze bir <see cref="ApplicationDbContext"/> döndürür (aynı
/// fiziksel DB'ye bağlı). InMemory <c>TestDbContextFactory</c> hâlâ
/// duruyor — pilot dışı sınıflar onu kullanmaya devam ediyor (hibrit).
/// </summary>
public class SqlServerTestFixture : IAsyncLifetime
{
    private readonly string _databaseName;
    private readonly string _connectionString;

    public string ConnectionString => _connectionString;

    public SqlServerTestFixture()
    {
        _databaseName = $"LexCalculusTest_{Guid.NewGuid():N}";
        _connectionString =
            $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};" +
            "Trusted_Connection=True;MultipleActiveResultSets=true";
    }

    /// <summary>Veritabanını oluşturur ve tüm migration'ları uygular.</summary>
    public async Task InitializeAsync()
    {
        await using var ctx = new ApplicationDbContext(BuildOptions(), new TestTenantContext());
        await ctx.Database.MigrateAsync();
    }

    /// <summary>Connection pool'u boşaltıp veritabanını düşürür (DROP DATABASE).</summary>
    public async Task DisposeAsync()
    {
        // EnsureDeletedAsync, açık connection varken "database in use" hatası
        // verebilir — pool'u boşalt, sonra düşür.
        SqlConnection.ClearAllPools();

        await using var ctx = new ApplicationDbContext(BuildOptions(), new TestTenantContext());
        await ctx.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// Aynı fiziksel DB'ye bağlı yeni bir <see cref="ApplicationDbContext"/>.
    /// Arrange/act/assert fazlarında ayrı context kullanımı için her çağrıda
    /// taze instance döner. <paramref name="tenantContext"/> verilmezse
    /// NoOp <see cref="TestTenantContext"/> kullanılır.
    /// </summary>
    public ApplicationDbContext CreateContext(ITenantContext? tenantContext = null)
        => new(BuildOptions(), tenantContext ?? new TestTenantContext());

    /// <summary>
    /// Convenience overload — testin perspektifini explicit kurar.
    /// CurrentTenantId=null, CurrentUserId=actAsUserId.
    /// </summary>
    public ApplicationDbContext CreateContext(int actAsUserId)
        => CreateContext(new TestTenantContext { CurrentUserId = actAsUserId });

    /// <summary>
    /// Pre-built options — InMemory <c>TestDbContextFactory.CreateOptions</c>
    /// ile simetrik; aynı DB'yi paylaşan farklı tenant context'leri için.
    /// </summary>
    public DbContextOptions<ApplicationDbContext> BuildOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(new AuditInterceptor())
            .Options;
}

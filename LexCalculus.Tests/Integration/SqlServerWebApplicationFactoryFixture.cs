using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexCalculus.Tests.Integration;

/// <summary>
/// Adım 5.8 P2 (charter Karar 10) — InMemory <see cref="WebApplicationFactoryFixture"/>'in
/// SQL Server LocalDB varyantı. Cookie auth pipeline'ını (TestAuth scheme'siz)
/// olduğu gibi bırakır; yalnızca veritabanını gerçek SQL Server LocalDB'ye
/// bağlar.
///
/// Her factory instance unique bir LocalDB veritabanı kullanır;
/// <see cref="InitializeAsync"/>'te migration uygulanır,
/// <see cref="DisposeAsync"/>'te veritabanı düşürülür.
/// </summary>
public sealed class SqlServerWebApplicationFactoryFixture
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString;

    public SqlServerWebApplicationFactoryFixture()
    {
        var databaseName = $"LexCalculusTest_{Guid.NewGuid():N}";
        _connectionString =
            $"Server=(localdb)\\mssqllocaldb;Database={databaseName};" +
            "Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=60";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Testing", "true");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(_connectionString);
                options.AddInterceptors(new AuditInterceptor());
            });
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await ctx.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ctx.Database.EnsureDeletedAsync();
        }

        Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        await base.DisposeAsync();
    }
}

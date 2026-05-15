using LexCalculus.Core.Messaging;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexCalculus.Tests.Integration;

/// <summary>
/// Adım 5.8 (charter Karar 10) — <see cref="TestAuthWebApplicationFactory"/>'nin
/// SQL Server LocalDB varyantı. Integration testler için gerçek SQL Server
/// semantiği sağlar (InMemory yerine).
///
/// Her factory instance unique bir LocalDB veritabanı (<c>LexCalculusTest_{guid}</c>)
/// kullanır; <see cref="InitializeAsync"/>'te migration uygulanır,
/// <see cref="DisposeAsync"/>'te veritabanı düşürülür. xUnit, IClassFixture
/// olarak kullanılan tipler <see cref="IAsyncLifetime"/> uygularsa bu metodları
/// otomatik çağırır.
///
/// TestAuth pattern (header-driven fake auth scheme) ve NoOp messaging notifier
/// — InMemory varyantla aynı — korunur.
/// </summary>
public sealed class SqlServerTestAuthWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName;
    private readonly string _connectionString;

    public SqlServerTestAuthWebApplicationFactory()
    {
        _databaseName = $"LexCalculusTest_{Guid.NewGuid():N}";
        _connectionString =
            $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};" +
            "Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=60";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Testing=true → Program.cs SqlServer DbContext + migration + Hangfire'ı atlar;
        // DbContext'i biz aşağıda LocalDB'ye register ediyoruz.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Testing", "true");

        builder.ConfigureServices(services =>
        {
            // Program.cs Testing modunda DbContext register etmiyor; yine de
            // defansif olarak varsa kaldır.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(_connectionString);
                options.AddInterceptors(new AuditInterceptor());
            });

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // SignalR notifier override (Faz 5.6): test ortamında Hub real-time
            // broadcast yapmasın — NoOp ile sessizce geçer.
            services.RemoveAll<IMessagingNotifier>();
            services.AddScoped<IMessagingNotifier, NoOpMessagingNotifier>();

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                options.DefaultSignInScheme = TestAuthHandler.SchemeName;
                options.DefaultSignOutScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    /// <summary>Host'u ayağa kaldırır, veritabanını oluşturur ve migration uygular.</summary>
    async Task IAsyncLifetime.InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await ctx.Database.MigrateAsync();
    }

    /// <summary>Veritabanını düşürür ve WebApplicationFactory kaynaklarını serbest bırakır.</summary>
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

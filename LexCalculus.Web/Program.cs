using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using LexCalculus.Infrastructure.Data.SeedData;
using LexCalculus.Infrastructure.HealthChecks;
using LexCalculus.Infrastructure.Repositories;
using LexCalculus.Infrastructure.Seo;
using LexCalculus.Web.Extensions;
using LexCalculus.Web.HealthChecks;
using LexCalculus.Web.ModelBinders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using StackExchange.Redis;

// =============================================================================
// BOOTSTRAP LOGGER — startup hatalarını yakalamak için
// =============================================================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting LexCalculus.Web");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // -------------------------------------------------------------------------
    // SERILOG — appsettings.json'dan Serilog konfigürasyonunu oku
    // -------------------------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // -------------------------------------------------------------------------
    // EF CORE — ApplicationDbContext + AuditInterceptor
    // -------------------------------------------------------------------------
    // AuditInterceptor singleton; aynı instance tüm DbContext'lere enjekte edilir
    builder.Services.AddSingleton<AuditInterceptor>();

    var isTesting = builder.Configuration.GetValue<bool>("Testing");
    if (!isTesting)
    {
        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });

            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());

            if (builder.Environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
    }

    // -------------------------------------------------------------------------
    // REPOSITORY & UNIT OF WORK
    // -------------------------------------------------------------------------
    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

    // -------------------------------------------------------------------------
    // CALCULATORS
    // -------------------------------------------------------------------------
    builder.Services.AddScoped<IFormulaParameterService, FormulaParameterService>();
    builder.Services.AddScoped<ILifeTableService, LifeTableService>();
    builder.Services.AddScoped<IActuarialService, ActuarialService>();
    builder.Services.AddCalculators();

    // -------------------------------------------------------------------------
    // SEO
    // -------------------------------------------------------------------------
    builder.Services.Configure<SeoSettings>(builder.Configuration.GetSection("SeoSettings"));
    builder.Services.AddSingleton<ISeoMetaProvider, DefaultSeoMetaProvider>();
    builder.Services.AddScoped<ISitemapBuilder, DefaultSitemapBuilder>();

    // -------------------------------------------------------------------------
    // HEALTH CHECKS
    // -------------------------------------------------------------------------
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>(
            name: "database",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "ready", "db" })
        .AddCheck<RedisHealthCheck>(
            name: "cache",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "ready", "cache" });

    // -------------------------------------------------------------------------
    // ASP.NET IDENTITY
    // -------------------------------------------------------------------------
    builder.Services
        .AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password
            builder.Configuration.GetSection("Identity:Password").Bind(options.Password);

            // Lockout — DefaultLockoutMinutes appsettings'ten dakika olarak gelir; TimeSpan'e çevir
            options.Lockout.MaxFailedAccessAttempts =
                builder.Configuration.GetValue<int>("Identity:Lockout:MaxFailedAccessAttempts");
            var lockoutMinutes = builder.Configuration.GetValue<int>("Identity:Lockout:DefaultLockoutMinutes");
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
            options.Lockout.AllowedForNewUsers = true;

            // User
            builder.Configuration.GetSection("Identity:User").Bind(options.User);

            // SignIn
            builder.Configuration.GetSection("Identity:SignIn").Bind(options.SignIn);
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // No-op email sender — real implementation comes in Phase 5
    builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender,
        Microsoft.AspNetCore.Identity.UI.Services.NoOpEmailSender>();

    // -------------------------------------------------------------------------
    // COOKIE — HttpOnly, Secure, SameSite.Strict
    // -------------------------------------------------------------------------
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Name = "LexCalculus.Auth";

        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";

        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

    // -------------------------------------------------------------------------
    // REDIS DISTRIBUTED CACHE — pre-flight probe; fall back to in-memory on failure
    // -------------------------------------------------------------------------
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    var useRedis = false;

    if (!string.IsNullOrWhiteSpace(redisConnection))
    {
        try
        {
            var configOptions = ConfigurationOptions.Parse(redisConnection);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectTimeout = 2000;
            configOptions.SyncTimeout = 2000;
            configOptions.AsyncTimeout = 2000;
            configOptions.ConnectRetry = 1;

            using var probe = ConnectionMultiplexer.Connect(configOptions);
            if (probe.IsConnected)
            {
                builder.Services.AddStackExchangeRedisCache(opts =>
                {
                    opts.ConfigurationOptions = configOptions;
                    opts.InstanceName = "LexCalculus:";
                });
                Log.Information("Redis cache configured at {Endpoint}", redisConnection);
                useRedis = true;
            }
            else
            {
                Log.Warning("Redis probe at {Endpoint} reported IsConnected=false; using in-memory distributed cache instead. " +
                            "This is expected in development without Redis; in production fix the connection.",
                            redisConnection);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Redis is not reachable at {Endpoint}; using in-memory distributed cache instead. " +
                            "This is expected in development without Redis; in production fix the connection.",
                            redisConnection);
        }
    }

    if (!useRedis)
    {
        builder.Services.AddDistributedMemoryCache();
        Log.Information("Using in-memory distributed cache (Redis disabled or unreachable).");
    }

    // -------------------------------------------------------------------------
    // MVC + RAZOR PAGES (Identity için lazım)
    // -------------------------------------------------------------------------
    var mvcBuilder = builder.Services.AddControllersWithViews(options =>
    {
        // Custom flexible decimal binder — accepts both "3.5" and "3,5"
        // Inserted at index 0 so it runs before the default binder
        options.ModelBinderProviders.Insert(0, new FlexibleDecimalModelBinderProvider());
    });
    builder.Services.AddResponseCaching();
    builder.Services.AddRazorPages();

    if (builder.Environment.IsDevelopment())
    {
        mvcBuilder.AddRazorRuntimeCompilation();
    }

    // -------------------------------------------------------------------------
    // BUILD
    // -------------------------------------------------------------------------
    var app = builder.Build();

    // -------------------------------------------------------------------------
    // PIPELINE
    // -------------------------------------------------------------------------
    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseResponseCaching();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    // Health endpoints
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = c => c.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
    });

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false, // No checks — just confirms process is up
        ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
    });

    // Areas (Identity dahil)
    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapRazorPages();

    // -------------------------------------------------------------------------
    // STARTUP MIGRATIONS + SEED
    // -------------------------------------------------------------------------
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var sp = scope.ServiceProvider;
        var startupLogger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            var dbContext = sp.GetRequiredService<ApplicationDbContext>();

            // Skip migrations for InMemory provider (integration tests)
            if (dbContext.Database.IsRelational())
            {
                startupLogger.LogInformation("Applying pending migrations…");
                await dbContext.Database.MigrateAsync();
                startupLogger.LogInformation("Migrations applied.");
            }
            else
            {
                startupLogger.LogInformation("Non-relational provider detected — skipping migrations.");
                await dbContext.Database.EnsureCreatedAsync();
            }

            var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
            await IdentitySeeder.SeedRolesAsync(roleManager, startupLogger);

            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            await IdentitySeeder.SeedAdminUserAsync(userManager, builder.Configuration, startupLogger);

            await CalculatorParameterSeeder.SeedAsync(dbContext, startupLogger);
            await LifeTableSeeder.SeedAsync(dbContext, startupLogger);
        }
        catch (Exception ex)
        {
            startupLogger.LogCritical(ex, "Database migration or seeding failed. Application will NOT start.");
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // RUN
    // -------------------------------------------------------------------------
    Log.Information("LexCalculus.Web is ready");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LexCalculus.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Integration testler için public partial Program {} pattern'i (Mvc.Testing kullanacak)
public partial class Program { }

using Hangfire;
using LexCalculus.Core.Admin.Dashboard;
using LexCalculus.Core.Email;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Notifications;
using LexCalculus.Core.Services;
using LexCalculus.Core.Services.Csv;
using LexCalculus.Infrastructure.Admin.Dashboard;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Infrastructure.Services.Csv;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Infrastructure.Email;
using LexCalculus.Infrastructure.Notifications;
using LexCalculus.Web.Infrastructure.Email;
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

    // Faz 3.7 — Multi-tenant altyapı
    // HttpContextAccessor ve ITenantContext, AddDbContext'ten ÖNCE register
    // edilmeli (DbContext constructor ITenantContext alır).
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<LexCalculus.Core.Services.ITenantContext,
        LexCalculus.Infrastructure.Tenancy.HttpTenantContext>();

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
    builder.Services.AddSingleton<IFormulaFreshnessChecker, FormulaFreshnessChecker>();
    builder.Services.AddScoped<ILifeTableService, LifeTableService>();
    builder.Services.AddScoped<IActuarialService, ActuarialService>();
    builder.Services.AddScoped<IInterestRateService, InterestRateService>();
    builder.Services.AddScoped<IThree095CommercialRateService, Three095CommercialRateService>();
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

    // Faz 3.7 — TenantId claim'i login sırasında ClaimsIdentity'ye ekle.
    // Identity'nin default factory'sini ezer (AddIdentity'den SONRA register).
    builder.Services.AddScoped<
        Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<ApplicationUser>,
        LexCalculus.Infrastructure.Identity.AppUserClaimsPrincipalFactory>();

    // -------------------------------------------------------------------------
    // HANGFIRE — background job processing (Phase 3.3)
    // -------------------------------------------------------------------------
    if (!isTesting)
    {
        builder.Services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180);
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();
            config.UseSqlServerStorage(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                new Hangfire.SqlServer.SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true,
                    SchemaName = "HangFire"
                });
        });

        builder.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 20);
            options.ServerName = $"lex-calculus-{Environment.MachineName}";
        });

        builder.Services.AddScoped<LexCalculus.Jobs.DataFreshness.DataFreshnessCheckJob>();
    }

    // -------------------------------------------------------------------------
    // EMAIL — Phase 3.3 Parça 2: provider seçimi appsettings'den
    // -------------------------------------------------------------------------
    builder.Services.Configure<EmailOptions>(
        builder.Configuration.GetSection(EmailOptions.SectionName));

    var emailProvider = builder.Configuration.GetValue<string>("Email:Provider") ?? "Logging";
    switch (emailProvider)
    {
        case "Smtp":
            builder.Services.AddScoped<IEmailService, SmtpEmailService>();
            break;
        case "SendGrid":
            builder.Services.AddScoped<IEmailService, SendGridEmailService>();
            break;
        default:
            builder.Services.AddScoped<IEmailService, LoggingEmailService>();
            break;
    }

    builder.Services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();

    // Notifications
    builder.Services.AddScoped<INotificationService, NotificationService>();

    // Admin dashboard summary
    builder.Services.AddScoped<IDashboardSummaryService, DashboardSummaryService>();

    // Admin: LifeTable yönetimi (calculator service'ten ayrı)
    builder.Services.AddScoped<ILifeTableAdminService, LifeTableAdminService>();
    builder.Services.AddScoped<ILifeTableCsvParser, LifeTableCsvParser>();

    // Admin: kullanıcı yönetimi (Faz 3.6)
    builder.Services.AddScoped<IUserAdminService, UserAdminService>();

    // Admin: tenant yönetimi (Faz 3.7 P2a/5)
    builder.Services.AddScoped<ITenantAdminService, TenantAdminService>();

    // Tenant request akışı (Faz 3.7 P2b/5) — kullanıcı talebi + admin onay/red
    builder.Services.AddScoped<ITenantRequestService, TenantRequestService>();

    // Session — admin KVKK banner gibi geçici, kullanıcı özel state için
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Name = "LexCalculus.Session";
    });

    // Authorization policies — Phase 3.1: AdminOnly for /admin area
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    });

    // Identity IEmailSender → IEmailService köprüsü (Faz 3.6 Parça 2b-i)
    // Önceki NoOpEmailSender DI'dan çıkarıldı; ihtiyaç olursa MS scaffold tipinin
    // namespace'i Microsoft.AspNetCore.Identity.UI.Services'te kalmaya devam eder.
    builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender,
        LexCalculus.Infrastructure.Identity.IdentityEmailSenderAdapter>();

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

    app.UseSession();

    app.UseAuthentication();
    app.UseAuthorization();

    // Hangfire dashboard — admin-only via cookie auth role check
    if (!isTesting)
    {
        app.UseHangfireDashboard("/admin/hangfire", new Hangfire.DashboardOptions
        {
            Authorization = new[] { new LexCalculus.Web.Infrastructure.Hangfire.AdminDashboardAuthorizationFilter() },
            DashboardTitle = "Lex Calculus — Background Jobs",
            StatsPollingInterval = 5000,
            DisplayStorageConnectionString = false
        });
    }

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
            await LexCalculus.Infrastructure.Persistence.Seed.FormulaParameterMetadataBackfill.BackfillAsync(dbContext);
            await LexCalculus.Infrastructure.Persistence.Seed.TUFESeedData.SeedAsync(dbContext);
            await LifeTableSeeder.SeedAsync(dbContext, startupLogger);
        }
        catch (Exception ex)
        {
            startupLogger.LogCritical(ex, "Database migration or seeding failed. Application will NOT start.");
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // RECURRING JOBS (Hangfire) — Phase 3.3 Parça 1: smoke test only
    // -------------------------------------------------------------------------
    if (!isTesting)
    {
        // Eski smoke-test job'ı sil (Parça 1'deki geçici alive log)
        Hangfire.RecurringJob.RemoveIfExists("smoke-test");

        // Veri tazelik kontrolü — her gün sabah 06:00 (Europe/Istanbul)
        TimeZoneInfo istanbulTz;
        try
        {
            istanbulTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows fallback
            istanbulTz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }

        Hangfire.RecurringJob.AddOrUpdate<LexCalculus.Jobs.DataFreshness.DataFreshnessCheckJob>(
            "data-freshness-check",
            job => job.ExecuteAsync(CancellationToken.None),
            "0 6 * * *",
            new Hangfire.RecurringJobOptions { TimeZone = istanbulTz });
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

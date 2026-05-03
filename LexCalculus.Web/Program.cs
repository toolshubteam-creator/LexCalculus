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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;

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
        builder.Services.AddScoped<LexCalculus.Jobs.Tenancy.ExpireInvitationsJob>();
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

    // KVKK hesap anonimize (Faz 5.1, charter Karar 6)
    builder.Services.AddScoped<IUserAnonymizationService, UserAnonymizationService>();

    // -------------------------------------------------------------------------
    // RATE LIMITING (Faz 5.2, charter Karar 7)
    // .NET built-in System.Threading.RateLimiting; per-user partition (UserId
    // claim), anonim kullanıcı için IP fallback. In-memory storage; multi-instance
    // gerekirse Faz 6+'da Redis backplane.
    // -------------------------------------------------------------------------
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, ct) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers.RetryAfter = "60";
            context.HttpContext.Response.ContentType = "application/json";
            const string message = "Çok fazla istek gönderdiniz. Lütfen biraz bekleyin.";
            await context.HttpContext.Response.WriteAsync(
                $"{{\"error\":\"{message}\"}}", ct);
        };

        static string GetPartitionKey(HttpContext httpContext)
        {
            var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId)) return $"user:{userId}";
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            return $"ip:{ip ?? "unknown"}";
        }

        // Yorum: 10/dk
        options.AddPolicy("comment", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetPartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        // Şikayet: 5/saat
        options.AddPolicy("report", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetPartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromHours(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        // Mesaj: 30/dk (Faz 5 Dalga B mesajlaşma için hazır)
        options.AddPolicy("message", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetPartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        // Bağlantı isteği: 20/saat
        options.AddPolicy("connection", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetPartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromHours(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        // Genel AJAX: 100/dk (catch-all — beğeni, görsel upload, vs.)
        options.AddPolicy("ajax-general", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetPartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

    // Admin: tenant yönetimi (Faz 3.7 P2a/5)
    builder.Services.AddScoped<ITenantAdminService, TenantAdminService>();

    // Tenant request akışı (Faz 3.7 P2b/5) — kullanıcı talebi + admin onay/red
    builder.Services.AddScoped<ITenantRequestService, TenantRequestService>();

    // Tenant invitation akışı (Faz 3.7 P3/5) — owner/admin → email davet
    builder.Services.AddScoped<ITenantInvitationService, TenantInvitationService>();

    // ActivityLog — sistem geneli denetim (Faz 3.8 P1/2)
    builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

    // Public profile (Faz 4.1) — slug üretimi + ileride avatar/public sayfa
    builder.Services.AddScoped<IPublicProfileService, PublicProfileService>();

    // Media (Faz 4.1 P2/3) — yerel disk depolama + ImageSharp upload pipeline
    builder.Services.AddScoped<LexCalculus.Core.Storage.IMediaStorage,
        LexCalculus.Infrastructure.Storage.LocalDiskMediaStorage>();
    builder.Services.AddScoped<IMediaUploadService, MediaUploadService>();

    // UserConnection (Faz 4.2 P1/3) — LinkedIn-tarzı bağlantı state machine
    builder.Services.AddScoped<IConnectionService, ConnectionService>();

    // UserBlock (Faz 4.3) — sessiz pattern engelleme + cascade
    builder.Services.AddScoped<IUserBlockService, UserBlockService>();

    // İçerik altyapısı (Faz 4.5) — kategori master + serbest tag
    builder.Services.AddScoped<IPostCategoryService, PostCategoryService>();
    builder.Services.AddScoped<IPostTagService, PostTagService>();

    // İçerik HTML sanitizer (Faz 4.6 P3) — Quill output Body için sıkı whitelist
    builder.Services.AddSingleton<Ganss.Xss.IHtmlSanitizer>(_ =>
    {
        var s = new Ganss.Xss.HtmlSanitizer();

        s.AllowedTags.Clear();
        s.AllowedTags.UnionWith(new[]
        {
            "p", "br", "strong", "em", "u", "s",
            "h2", "h3", "h4",
            "ul", "ol", "li",
            "a", "blockquote", "code", "pre",
            "img", "figure", "figcaption"
        });

        s.AllowedAttributes.Clear();
        s.AllowedAttributes.UnionWith(new[]
        {
            "href", "title",
            "src", "alt", "width", "height",
            "class"
        });

        // Inline CSS YASAK
        s.AllowedCssProperties.Clear();
        s.AllowedAtRules.Clear();

        // URL şeması whitelist
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });

        return s;
    });

    // Kullanıcı makalesi (Faz 4.6 P1) — Draft/Published state machine + tag sync
    builder.Services.AddScoped<IUserPostService, UserPostService>();

    // Yorum + beğeni (Faz 4.9 P1)
    builder.Services.AddSingleton<ICommentSanitizer, CommentSanitizer>();
    builder.Services.AddScoped<IPostCommentService, PostCommentService>();
    builder.Services.AddScoped<IPostLikeService, PostLikeService>();

    // İçerik şikayet (Faz 4.10 P1)
    builder.Services.AddScoped<IContentReportService, ContentReportService>();

    // Faz 4.9 P2 — AJAX endpoint'lerin Razor partial render etmesi için
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<LexCalculus.Web.Infrastructure.Rendering.IPartialRenderer,
        LexCalculus.Web.Infrastructure.Rendering.PartialRenderer>();

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

    // AJAX endpoint'ler (Faz 4.8+) için CSRF token header adı.
    // Form post'larında __RequestVerificationToken otomatik bind edilir;
    // fetch/XHR isteklerinde JS bu header'ı manuel set eder.
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
    });

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

    // Rate limiter (Faz 5.2) — auth sonrası yerleşmeli (User.Identity dolu olsun
    // ki per-user partition key resolve edilebilsin).
    app.UseRateLimiter();

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
            await PostCategorySeeder.SeedAsync(dbContext, startupLogger);
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

        // Tenant davet süresi dolanları Expired'e çek — her gün 03:00 (Europe/Istanbul)
        Hangfire.RecurringJob.AddOrUpdate<LexCalculus.Jobs.Tenancy.ExpireInvitationsJob>(
            "expire-invitations",
            job => job.ExecuteAsync(CancellationToken.None),
            "0 3 * * *",
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

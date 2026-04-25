using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using LexCalculus.Infrastructure.Data.SeedData;
using LexCalculus.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

    // -------------------------------------------------------------------------
    // REPOSITORY & UNIT OF WORK
    // -------------------------------------------------------------------------
    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

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
    // REDIS DISTRIBUTED CACHE
    // -------------------------------------------------------------------------
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redisConnection))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "LexCalculus:";
        });
    }
    else
    {
        // Redis yoksa in-memory'e düş (dev convenience)
        builder.Services.AddDistributedMemoryCache();
        Log.Warning("Redis connection string is not configured. Falling back to in-memory distributed cache.");
    }

    // -------------------------------------------------------------------------
    // MVC + RAZOR PAGES (Identity için lazım)
    // -------------------------------------------------------------------------
    var mvcBuilder = builder.Services.AddControllersWithViews();
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

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

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

            startupLogger.LogInformation("Applying pending migrations…");
            await dbContext.Database.MigrateAsync();
            startupLogger.LogInformation("Migrations applied.");

            var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
            await IdentitySeeder.SeedRolesAsync(roleManager, startupLogger);

            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            await IdentitySeeder.SeedAdminUserAsync(userManager, builder.Configuration, startupLogger);
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

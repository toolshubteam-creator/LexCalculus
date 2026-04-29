using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Data.SeedData;

/// <summary>
/// Seeds the three built-in roles (Admin, Editor, Kullanici) and the
/// initial Admin user from configuration. Idempotent — safe to run on
/// every startup.
/// Faz 3.6: Premium/Free rolleri kaldırıldı; default rol Kullanici.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedRolesAsync(
        RoleManager<ApplicationRole> roleManager,
        ILogger logger,
        CancellationToken ct = default)
    {
        var roles = new[]
        {
            new { Name = nameof(UserRole.Admin),     Description = "Full administrative access — system, content, users." },
            new { Name = nameof(UserRole.Editor),    Description = "Content management — pages, posts, media, comments moderation." },
            new { Name = nameof(UserRole.Kullanici), Description = "Standart kullanıcı — hesaplayıcılara erişim, kişisel hesap geçmişi." }
        };

        foreach (var role in roles)
        {
            ct.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(role.Name)) continue;

            var result = await roleManager.CreateAsync(new ApplicationRole
            {
                Name = role.Name,
                Description = role.Description
            });

            if (result.Succeeded)
                logger.LogInformation("Seeded role: {RoleName}", role.Name);
            else
                logger.LogError("Failed to seed role {RoleName}: {Errors}",
                    role.Name, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    public static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct = default)
    {
        var email = configuration["AdminUser:Email"];
        var password = configuration["AdminUser:Password"];
        var fullName = configuration["AdminUser:FullName"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("AdminUser:Email or AdminUser:Password is missing. Skipping admin seed.");
            return;
        }

        if (password == "PLACEHOLDER_USE_USER_SECRETS")
        {
            logger.LogError("AdminUser:Password is still set to placeholder. Use 'dotnet user-secrets set' to provide a real password. Skipping admin seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            logger.LogDebug("Admin user already exists: {Email}", email);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            LockoutEnabled = false
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to create admin user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, nameof(UserRole.Admin));
        logger.LogInformation("Seeded admin user: {Email}", email);
    }
}

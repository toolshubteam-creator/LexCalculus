using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Data.SeedData;

/// <summary>
/// Seeds 10 başlangıç makale kategorisi. Idempotent — slug eşleşen kategori
/// varsa atlanır. Faz 4.5.
/// </summary>
public static class PostCategorySeeder
{
    private static readonly (string Name, int Order)[] Seeds =
    {
        ("İş Hukuku", 1),
        ("Aile Hukuku", 2),
        ("Ceza Hukuku", 3),
        ("Borçlar Hukuku", 4),
        ("Gayrimenkul Hukuku", 5),
        ("Vergi Hukuku", 6),
        ("Ticaret Hukuku", 7),
        ("Miras Hukuku", 8),
        ("KVKK ve Veri Koruma", 9),
        ("Genel", 10)
    };

    public static async Task SeedAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var existingSlugs = await db.PostCategories
            .Select(c => c.Slug)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingSlugs, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (name, order) in Seeds)
        {
            ct.ThrowIfCancellationRequested();

            var slug = SlugHelper.Generate(name);
            if (existingSet.Contains(slug)) continue;

            db.PostCategories.Add(new PostCategory
            {
                Name = name,
                Slug = slug,
                DisplayOrder = order,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} post categories.", added);
        }
        else
        {
            logger.LogDebug("Post categories seed skipped — all slugs already present.");
        }
    }
}

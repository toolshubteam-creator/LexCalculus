using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Tests.TestHelpers;
using Xunit;

namespace LexCalculus.Tests.Interceptors;

public class AuditInterceptorTests
{
    [Fact]
    public async Task ApplicationUser_Insert_Sets_CreatedAt()
    {
        await using var ctx = TestDbContextFactory.Create();
        var beforeUtc = DateTime.UtcNow;

        var user = new ApplicationUser
        {
            UserName = "audit@test.local",
            Email = "audit@test.local"
        };

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        user.CreatedAt.Should().BeAfter(beforeUtc.AddSeconds(-1));
        user.CreatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task ApplicationUser_Insert_Does_Not_Override_Explicit_CreatedAt()
    {
        await using var ctx = TestDbContextFactory.Create();
        var explicitTime = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var user = new ApplicationUser
        {
            UserName = "explicit@test.local",
            Email = "explicit@test.local",
            CreatedAt = explicitTime
        };

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        user.CreatedAt.Should().Be(explicitTime, "interceptor must respect explicit CreatedAt set by caller (e.g. seeder)");
    }

    [Fact]
    public async Task BaseEntity_Modify_Updates_UpdatedAt_But_Not_CreatedAt()
    {
        await using var ctx = TestDbContextFactory.Create();
        var profile = new UserProfile { UserId = 100, DisplayName = "Audit Original" };
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        var originalCreatedAt = profile.CreatedAt;
        profile.UpdatedAt.Should().BeNull();

        // small delay to make Modified timestamp distinguishable
        await Task.Delay(10);

        profile.DisplayName = "Audit Modified";
        await ctx.SaveChangesAsync();

        profile.UpdatedAt.Should().NotBeNull();
        profile.CreatedAt.Should().Be(originalCreatedAt);
        profile.UpdatedAt!.Value.Should().BeAfter(originalCreatedAt);
    }
}

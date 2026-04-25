using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Repositories;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Repositories;

public class RepositoryTests
{
    [Fact]
    public async Task AddAsync_Sets_CreatedAt_To_UtcNow()
    {
        // Arrange
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);
        var profile = new UserProfile { UserId = 1, DisplayName = "Test User" };
        var beforeUtc = DateTime.UtcNow;

        // Act
        await repo.AddAsync(profile);
        await ctx.SaveChangesAsync();

        // Assert
        profile.CreatedAt.Should().BeAfter(beforeUtc.AddSeconds(-1));
        profile.CreatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        profile.UpdatedAt.Should().BeNull("UpdatedAt is null on creation");
        profile.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Entity_When_Exists()
    {
        // Arrange
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);
        var profile = new UserProfile { UserId = 2, DisplayName = "Existing" };
        await repo.AddAsync(profile);
        await ctx.SaveChangesAsync();

        // Act
        var found = await repo.GetByIdAsync(profile.Id);

        // Assert
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("Existing");
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_When_Missing()
    {
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);

        var result = await repo.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Sets_IsDeleted_True_And_Updates_UpdatedAt()
    {
        // Arrange
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);
        var profile = new UserProfile { UserId = 3, DisplayName = "Soft Delete Me" };
        await repo.AddAsync(profile);
        await ctx.SaveChangesAsync();
        var idToDelete = profile.Id;

        // Act
        await repo.DeleteAsync(idToDelete);
        await ctx.SaveChangesAsync();

        // Assert — query with IgnoreQueryFilters since soft-deleted rows are filtered out
        var deleted = await ctx.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == idToDelete);

        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
        deleted.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_Excludes_SoftDeleted_Entities()
    {
        // Arrange
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);

        var keep = new UserProfile { UserId = 10, DisplayName = "Keep" };
        var remove = new UserProfile { UserId = 11, DisplayName = "Remove" };
        await repo.AddAsync(keep);
        await repo.AddAsync(remove);
        await ctx.SaveChangesAsync();

        await repo.DeleteAsync(remove.Id);
        await ctx.SaveChangesAsync();

        // Act
        var all = await repo.GetAllAsync();

        // Assert
        all.Should().HaveCount(1);
        all[0].DisplayName.Should().Be("Keep");
    }

    [Fact]
    public async Task Update_Sets_UpdatedAt_On_Save()
    {
        // Arrange
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);
        var profile = new UserProfile { UserId = 20, DisplayName = "Original" };
        await repo.AddAsync(profile);
        await ctx.SaveChangesAsync();

        var initialCreatedAt = profile.CreatedAt;
        profile.UpdatedAt.Should().BeNull(); // sanity

        // Act
        profile.DisplayName = "Modified";
        repo.Update(profile);
        await ctx.SaveChangesAsync();

        // Assert
        profile.UpdatedAt.Should().NotBeNull();
        profile.CreatedAt.Should().Be(initialCreatedAt, "CreatedAt must be immutable after creation");
    }

    [Fact]
    public async Task AddAsync_Throws_On_Null_Entity()
    {
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);

        var act = async () => await repo.AddAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DeleteAsync_Is_Idempotent_For_Already_Deleted()
    {
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);
        var profile = new UserProfile { UserId = 30, DisplayName = "Twice Deleted" };
        await repo.AddAsync(profile);
        await ctx.SaveChangesAsync();

        await repo.DeleteAsync(profile.Id);
        await ctx.SaveChangesAsync();

        // Act — second delete on same id should not throw
        var act = async () => { await repo.DeleteAsync(profile.Id); await ctx.SaveChangesAsync(); };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_On_Missing_Id_Is_NoOp()
    {
        await using var ctx = TestDbContextFactory.Create();
        var repo = new Repository<UserProfile>(ctx);

        var act = async () => { await repo.DeleteAsync(99999); await ctx.SaveChangesAsync(); };

        await act.Should().NotThrowAsync();
    }
}

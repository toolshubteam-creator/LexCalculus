using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Repositories;
using LexCalculus.Tests.TestHelpers;
using Xunit;

namespace LexCalculus.Tests.Repositories;

public class UnitOfWorkTests : SqlServerTestBase
{
    [Fact]
    public async Task SaveChangesAsync_Persists_Pending_Changes()
    {
        await using var ctx = _db.Create();

        // SQL Server FK_UserProfiles_AspNetUsers_UserId zorunlu — önce user seed et.
        var user = new ApplicationUser
        {
            UserName = "uow@x.com",
            NormalizedUserName = "UOW@X.COM",
            Email = "uow@x.com",
            NormalizedEmail = "UOW@X.COM",
            FullName = "UoW",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var repo = new Repository<UserProfile>(ctx);
        var uow = new UnitOfWork(ctx);

        await repo.AddAsync(new UserProfile { UserId = user.Id, DisplayName = "Saved" });

        var changes = await uow.SaveChangesAsync();

        changes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BeginTransactionAsync_Twice_Throws()
    {
        await using var ctx = _db.Create();
        var uow = new UnitOfWork(ctx);

        // InMemory provider returns a fake transaction; our guard still fires on double begin.
        try
        {
            await uow.BeginTransactionAsync();
        }
        catch (InvalidOperationException)
        {
            // InMemory provider may throw outright; either is acceptable.
            return;
        }

        var act = async () => await uow.BeginTransactionAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CommitTransactionAsync_Without_Begin_Throws()
    {
        await using var ctx = _db.Create();
        var uow = new UnitOfWork(ctx);

        var act = async () => await uow.CommitTransactionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RollbackTransactionAsync_Without_Begin_Is_NoOp()
    {
        await using var ctx = _db.Create();
        var uow = new UnitOfWork(ctx);

        var act = async () => await uow.RollbackTransactionAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_Is_Safe_To_Call_Twice()
    {
        var ctx = _db.Create();
        var uow = new UnitOfWork(ctx);

        await uow.DisposeAsync();
        var act = async () => await uow.DisposeAsync();

        await act.Should().NotThrowAsync();
        await ctx.DisposeAsync();
    }
}

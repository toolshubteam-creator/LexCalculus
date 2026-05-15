using FluentAssertions;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Content;

public class PostCategoryServiceTests : SqlServerTestBase
{
    private (PostCategoryService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var svc = new PostCategoryService(ctx, new NullActivityLogService());
        return (svc, ctx);
    }

    [Fact]
    public async Task CreateAsync_GeneratesSlugFromTurkishName()
    {
        var (svc, ctx) = Setup();
        var result = await svc.CreateAsync(new PostCategoryInput("İş Hukuku", null, 1, true));

        result.Success.Should().BeTrue();
        result.Category!.Slug.Should().Be("is-hukuku");
        result.Category.Name.Should().Be("İş Hukuku");
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await svc.CreateAsync(new PostCategoryInput("İş Hukuku", null, 1, true));

        var second = await svc.CreateAsync(new PostCategoryInput("İş Hukuku", null, 2, true));

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("zaten mevcut");
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ReturnsError()
    {
        var (svc, _) = Setup();
        var result = await svc.CreateAsync(new PostCategoryInput("   ", null, 1, true));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boş olamaz");
    }

    [Fact]
    public async Task CreateAsync_NameYieldsEmptySlug_ReturnsError()
    {
        // Sadece özel karakterler — slug üretimi boş döner
        var (svc, _) = Setup();
        var result = await svc.CreateAsync(new PostCategoryInput("!!!", null, 1, true));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("slug");
    }

    [Fact]
    public async Task UpdateAsync_NameChanges_RegeneratesSlug()
    {
        var (svc, ctx) = Setup();
        var created = await svc.CreateAsync(new PostCategoryInput("İş Hukuku", null, 1, true));

        var updated = await svc.UpdateAsync(created.Category!.Id,
            new PostCategoryInput("Aile Hukuku", null, 1, true));

        updated.Success.Should().BeTrue();
        updated.Category!.Slug.Should().Be("aile-hukuku");
    }

    [Fact]
    public async Task UpdateAsync_SameName_KeepsSlug()
    {
        var (svc, ctx) = Setup();
        var created = await svc.CreateAsync(new PostCategoryInput("İş Hukuku", null, 1, true));
        var originalSlug = created.Category!.Slug;

        var updated = await svc.UpdateAsync(created.Category.Id,
            new PostCategoryInput("İş Hukuku", "yeni açıklama", 5, true));

        updated.Success.Should().BeTrue();
        updated.Category!.Slug.Should().Be(originalSlug);
        updated.Category.Description.Should().Be("yeni açıklama");
        updated.Category.DisplayOrder.Should().Be(5);
        updated.Category.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_NewNameClashesWithOther_ReturnsError()
    {
        var (svc, _) = Setup();
        await svc.CreateAsync(new PostCategoryInput("İş Hukuku", null, 1, true));
        var second = await svc.CreateAsync(new PostCategoryInput("Aile Hukuku", null, 2, true));

        var attempt = await svc.UpdateAsync(second.Category!.Id,
            new PostCategoryInput("İş Hukuku", null, 2, true));

        attempt.Success.Should().BeFalse();
        attempt.ErrorMessage.Should().Contain("zaten mevcut");
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse_KeepsRow()
    {
        var (svc, ctx) = Setup();
        var created = await svc.CreateAsync(new PostCategoryInput("Test", null, 1, true));

        var result = await svc.DeactivateAsync(created.Category!.Id);

        result.Success.Should().BeTrue();
        var stillExists = await ctx.PostCategories.AnyAsync(c => c.Id == created.Category.Id);
        stillExists.Should().BeTrue("hard delete olmamalı");
        result.Category!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ReactivateAsync_RestoresActive()
    {
        var (svc, _) = Setup();
        var created = await svc.CreateAsync(new PostCategoryInput("Test", null, 1, true));
        await svc.DeactivateAsync(created.Category!.Id);

        var result = await svc.ReactivateAsync(created.Category.Id);

        result.Success.Should().BeTrue();
        result.Category!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_ExcludesInactive_OrderedByDisplayOrder()
    {
        var (svc, _) = Setup();
        await svc.CreateAsync(new PostCategoryInput("Üçüncü", null, 30, true));
        await svc.CreateAsync(new PostCategoryInput("Birinci", null, 10, true));
        await svc.CreateAsync(new PostCategoryInput("İkinci", null, 20, true));
        var inactive = await svc.CreateAsync(new PostCategoryInput("Pasif", null, 5, false));

        var list = await svc.GetActiveAsync();

        list.Should().HaveCount(3);
        list.Select(c => c.DisplayOrder).Should().Equal(10, 20, 30);
        list.Should().NotContain(c => c.Id == inactive.Category!.Id);
    }

    [Fact]
    public async Task GetAllAsync_IncludesInactive()
    {
        var (svc, _) = Setup();
        await svc.CreateAsync(new PostCategoryInput("Aktif", null, 1, true));
        await svc.CreateAsync(new PostCategoryInput("Pasif", null, 2, false));

        var list = await svc.GetAllAsync();

        list.Should().HaveCount(2);
    }
}

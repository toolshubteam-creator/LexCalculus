using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Extensions;
using Xunit;

namespace LexCalculus.Tests.UserManagement;

public class ApplicationUserDisplayExtensionsTests
{
    [Fact]
    public void GetDisplayNameOrAnonymized_NullUser_ReturnsAnonymized()
    {
        ApplicationUser? user = null;
        user.GetDisplayNameOrAnonymized().Should().Be("Silinmiş Kullanıcı");
    }

    [Fact]
    public void GetDisplayNameOrAnonymized_InactiveUser_ReturnsAnonymized()
    {
        var user = new ApplicationUser
        {
            IsActive = false,
            UserName = "x@x.com",
            Profile = new UserProfile { DisplayName = "Esat" }
        };
        user.GetDisplayNameOrAnonymized().Should().Be("Silinmiş Kullanıcı");
    }

    [Fact]
    public void GetDisplayNameOrAnonymized_ActiveWithProfile_ReturnsDisplayName()
    {
        var user = new ApplicationUser
        {
            IsActive = true,
            UserName = "x@x.com",
            Profile = new UserProfile { DisplayName = "Esat Bey" }
        };
        user.GetDisplayNameOrAnonymized().Should().Be("Esat Bey");
    }

    [Fact]
    public void GetDisplayNameOrAnonymized_ActiveNoProfile_ReturnsUserName()
    {
        var user = new ApplicationUser
        {
            IsActive = true,
            UserName = "fallback@x.com",
            Profile = null
        };
        user.GetDisplayNameOrAnonymized().Should().Be("fallback@x.com");
    }

    [Fact]
    public void GetDisplayNameOrAnonymized_ActiveNoProfileNoUserName_ReturnsKullanici()
    {
        var user = new ApplicationUser { IsActive = true, UserName = null };
        user.GetDisplayNameOrAnonymized().Should().Be("Kullanıcı");
    }

    [Fact]
    public void GetDisplayNameOrAnonymized_BlankProfileDisplayName_FallsBackToUserName()
    {
        var user = new ApplicationUser
        {
            IsActive = true,
            UserName = "x@x.com",
            Profile = new UserProfile { DisplayName = "   " }
        };
        user.GetDisplayNameOrAnonymized().Should().Be("x@x.com");
    }

    [Fact]
    public void IsAnonymizedOrInactive_NullUser_ReturnsTrue()
    {
        ApplicationUser? user = null;
        user.IsAnonymizedOrInactive().Should().BeTrue();
    }

    [Fact]
    public void IsAnonymizedOrInactive_ActiveUser_ReturnsFalse()
    {
        var user = new ApplicationUser { IsActive = true };
        user.IsAnonymizedOrInactive().Should().BeFalse();
    }

    [Fact]
    public void GetPublicSlugOrNull_InactiveUser_ReturnsNull()
    {
        var user = new ApplicationUser
        {
            IsActive = false,
            Profile = new UserProfile { PublicSlug = "esat-bey" }
        };
        user.GetPublicSlugOrNull().Should().BeNull();
    }

    [Fact]
    public void GetPublicSlugOrNull_ActiveUser_ReturnsSlug()
    {
        var user = new ApplicationUser
        {
            IsActive = true,
            Profile = new UserProfile { PublicSlug = "esat-bey" }
        };
        user.GetPublicSlugOrNull().Should().Be("esat-bey");
    }
}

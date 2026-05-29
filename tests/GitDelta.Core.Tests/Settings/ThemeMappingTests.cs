using GitDelta.Core.Models;
using GitDelta.Core.Settings;
using Shouldly;
using Xunit;

namespace GitDelta.Core.Tests.Settings;

public class ThemeMappingTests
{
    [Fact]
    public void ResolveIsDark_Light_ReturnsFalse()
    {
        ThemeMapping.ResolveIsDark(AppTheme.Light, systemIsDark: true).ShouldBeFalse();
    }

    [Fact]
    public void ResolveIsDark_Dark_ReturnsTrue()
    {
        ThemeMapping.ResolveIsDark(AppTheme.Dark, systemIsDark: false).ShouldBeTrue();
    }

    [Fact]
    public void ResolveIsDark_System_FollowsSystemDark()
    {
        ThemeMapping.ResolveIsDark(AppTheme.System, systemIsDark: true).ShouldBeTrue();
    }

    [Fact]
    public void ResolveIsDark_System_FollowsSystemLight()
    {
        ThemeMapping.ResolveIsDark(AppTheme.System, systemIsDark: false).ShouldBeFalse();
    }

    [Theory]
    [InlineData(AppTheme.Light, AppTheme.Dark)]
    [InlineData(AppTheme.Dark, AppTheme.Light)]
    [InlineData(AppTheme.System, AppTheme.Dark)]
    public void Toggle_FromExplicitOrSystem_FlipsToOppositeOfCurrentEffective(AppTheme current, AppTheme expected)
    {
        // current effective dark? Light=>false, Dark=>true, System(systemIsDark:false)=>false.
        ThemeMapping.Toggle(current, systemIsDark: false).ShouldBe(expected);
    }
}

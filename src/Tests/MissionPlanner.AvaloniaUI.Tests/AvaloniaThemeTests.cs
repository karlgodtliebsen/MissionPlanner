using Avalonia.Styling;
using MissionPlanner.Themes;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the persisted Avalonia theme contract and application style composition.</summary>
public sealed class AvaloniaThemeTests
{
    /// <summary>Verifies every persisted identifier is unique and resolves to itself.</summary>
    [Fact]
    public void CatalogIdentifiersAreUniqueAndResolvable()
    {
        var items = AvaloniaThemeCatalog.Items;

        Assert.Equal(items.Count, items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(items, item => Assert.Same(item, AvaloniaThemeCatalog.Resolve(item.Id)));
        Assert.Same(ThemeVariant.Default, AvaloniaThemeCatalog.Resolve("system").Theme);
        Assert.Same(ThemeVariant.Light, AvaloniaThemeCatalog.Resolve("light").Theme);
        Assert.Same(ThemeVariant.Dark, AvaloniaThemeCatalog.Resolve("dark").Theme);
    }

    /// <summary>Verifies every additional color family is selectable in both appearances.</summary>
    [Theory]
    [InlineData("red", "Red")]
    [InlineData("pink", "Pink")]
    [InlineData("violet", "Violet")]
    [InlineData("indigo", "Indigo")]
    [InlineData("yellow", "Yellow")]
    [InlineData("lime", "Lime")]
    [InlineData("light-green", "Light Green")]
    [InlineData("light-blue", "Light Blue")]
    [InlineData("teal", "Teal")]
    [InlineData("cyan", "Cyan")]
    [InlineData("green", "Green")]
    [InlineData("grey", "Grey")]
    [InlineData("ai-purple", "AI Purple")]
    public void AdditionalPalettesHaveBothAppearances(string id, string name)
    {
        var dark = AvaloniaThemeCatalog.Resolve($"dark-{id}");
        var light = AvaloniaThemeCatalog.Resolve($"light-{id}");
        Assert.Equal($"Dark {name}", dark.Name);
        Assert.Equal($"Light {name}", light.Name);
        Assert.Equal(ThemeVariant.Dark, dark.Theme.InheritVariant);
        Assert.Equal(ThemeVariant.Light, light.Theme.InheritVariant);
    }

    /// <summary>Verifies compatibility identifiers migrate to the current persisted values.</summary>
    [Theory]
    [InlineData("mission-light", "light")]
    [InlineData("mission-dark", "dark")]
    [InlineData("unknown", "system")]
    [InlineData(null, "system")]
    public void LegacyOrUnknownIdentifiersResolveSafely(string? identifier, string expected)
    {
        Assert.Equal(expected, AvaloniaThemeCatalog.Resolve(identifier).Id);
    }

    /// <summary>Verifies global AXAML composes Semi, Ursa, and shared application styles.</summary>
    [Fact]
    public void ApplicationAxamlContainsCurrentThemeComposition()
    {
        var appAxaml = File.ReadAllText(RepositoryPath(
            "src", "UI", "MissionPlanner.App", "App.axaml"));

        Assert.Contains("<semi:SemiTheme", appAxaml, StringComparison.Ordinal);
        Assert.Contains("<semi:UrsaSemiTheme", appAxaml, StringComparison.Ordinal);
        Assert.Contains("<themes:MissionPlannerTheme", appAxaml, StringComparison.Ordinal);
        Assert.Contains("avares://MissionPlanner.Themes/Resources/Styles/SharedStyles.axaml", appAxaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AppThemeBinding", appAxaml, StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }
}

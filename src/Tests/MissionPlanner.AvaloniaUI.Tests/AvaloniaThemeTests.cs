using Avalonia.Styling;
using MissionPlanner.App.Views.Navigation;

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
            "src", "UI", "AvaloniaUI", "MissionPlanner.AvaloniaUI.App", "App.axaml"));

        Assert.Contains("<semi:SemiTheme", appAxaml, StringComparison.Ordinal);
        Assert.Contains("<semi:UrsaSemiTheme", appAxaml, StringComparison.Ordinal);
        Assert.Contains("/Resources/Styles/SharedStyles.axaml", appAxaml, StringComparison.Ordinal);
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

using Avalonia.Styling;
using MissionPlanner.Themes;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the persisted Avalonia theme contract and application style composition.</summary>
public sealed class AvaloniaThemeTests
{
    /// <summary>Named variants keep their ordinary base and survive the production settings serializer.</summary>
    [Theory]
    [InlineData("dark-orange", true)]
    [InlineData("light-orange", false)]
    [InlineData("system", false)]
    [InlineData("night-sky", true)]
    public async Task AppearanceSelectionSurvivesReload(string identifier, bool isDark)
    {
        var store = NSubstitute.Substitute.For<MissionPlanner.Core.ConfigTuning.Planner.IPlannerSettingsStore>();
        string? document = null;
        store.ReadAsync(Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult(document));
        store.WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            document = call.Arg<string>();
            return ValueTask.CompletedTask;
        });
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MissionPlanner.Core.ConfigTuning.Planner.PlannerSettingsService>.Instance;
        var service = new MissionPlanner.Core.ConfigTuning.Planner.PlannerSettingsService(store, logger);
        await service.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True((await service.SaveAsync(service.Current with
        {
            Appearance = service.Current.Appearance with
            {
                ThemeId = identifier
            }
        }, TestContext.Current.CancellationToken)).Success);
        var reloaded = new MissionPlanner.Core.ConfigTuning.Planner.PlannerSettingsService(store, logger);
        await reloaded.InitializeAsync(TestContext.Current.CancellationToken);
        var item = AvaloniaThemeCatalog.Resolve(reloaded.Current.Appearance.ThemeId);
        Assert.Equal(identifier, item.Id);
        Assert.Equal(isDark, MissionPlanner.Themes.MissionPlannerThemeVariants.IsDark(item.Theme));
    }

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
            "src", "UI", "MissionPlanner.App", "App.axaml"));

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

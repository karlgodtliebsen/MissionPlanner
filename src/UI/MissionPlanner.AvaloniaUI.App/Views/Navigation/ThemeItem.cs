using Avalonia.Styling;

using Avalonia;
using Semi.Avalonia;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

/// <summary>Describes an Avalonia theme that can be selected by the user.</summary>
public sealed class ThemeItem(string id, string name, ThemeVariant theme)
{
    /// <summary>Gets the stable identifier stored in Planner settings.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the user-facing theme name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the Avalonia theme variant.</summary>
    public ThemeVariant Theme { get; } = theme;
}

/// <summary>Maps persisted Planner theme identifiers to Avalonia and Semi theme variants.</summary>
public static class AvaloniaThemeCatalog
{
    private static readonly IReadOnlyList<ThemeItem> items =
    [
        new("system", "Default", ThemeVariant.Default),
        new("light", "Light", ThemeVariant.Light),
        new("dark", "Dark", ThemeVariant.Dark),
        new("aquatic", "Aquatic", SemiTheme.Aquatic),
        new("desert", "Desert", SemiTheme.Desert),
        new("dusk", "Dusk", SemiTheme.Dusk),
        new("night-sky", "Night Sky", SemiTheme.NightSky)
    ];

    /// <summary>Gets all themes supported by the Avalonia application.</summary>
    public static IReadOnlyList<ThemeItem> Items => items;

    /// <summary>Raised after the application theme changes.</summary>
    public static event EventHandler<ThemeItem>? ThemeChanged;

    /// <summary>Finds a theme by its persisted identifier, including legacy identifiers.</summary>
    public static ThemeItem Resolve(string? id)
    {
        var normalized = id?.Trim().ToLowerInvariant() switch
        {
            "mission-light" => "light",
            "mission-dark" => "dark",
            var value => value
        };

        return items.FirstOrDefault(item => item.Id == normalized) ?? items[0];
    }

    /// <summary>Applies a theme to Avalonia and notifies active theme selectors.</summary>
    public static void Apply(ThemeItem theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = theme.Theme;
        }

        ThemeChanged?.Invoke(null, theme);
    }
}

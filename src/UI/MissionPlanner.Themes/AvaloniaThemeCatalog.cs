using Avalonia;
using Avalonia.Styling;
using Semi.Avalonia;

namespace MissionPlanner.Themes;

/// <summary>
/// Maps persisted Planner theme identifiers to Avalonia and Semi theme variants.
/// </summary>
public static class AvaloniaThemeCatalog
{
    private static Application? colorApplication;
    private static ThemeColorCustomizer? colors;

    /// <summary>Gets the live color preferences for the current application.</summary>
    public static ThemeColorCustomizer Colors
    {
        get
        {
            var application = Application.Current ?? throw new InvalidOperationException("Avalonia is not initialized.");
            if (!ReferenceEquals(colorApplication, application))
            {
                if (colorApplication is not null) colorApplication.PropertyChanged -= OnApplicationPropertyChanged;
                colors?.Restore();
                colorApplication = application;
                colors = new ThemeColorCustomizer(application);
                application.PropertyChanged += OnApplicationPropertyChanged;
            }
            return colors!;
        }
    }

    private static void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == Application.ActualThemeVariantProperty && colors is not null)
            colors.Apply(colors.ThemeId);
    }

    private static readonly IReadOnlyList<ThemeItem> items =
    [
        new("system", "Default", ThemeVariant.Default),
        new("light", "Light", ThemeVariant.Light),
        new("dark", "Dark", ThemeVariant.Dark),
        new("dark-orange", "Dark Orange", MissionPlannerThemeVariants.DarkOrange),
        new("light-orange", "Light Orange", MissionPlannerThemeVariants.LightOrange),
        new("dark-purple", "Dark Purple", MissionPlannerThemeVariants.DarkPurple),
        new("light-purple", "Light Purple", MissionPlannerThemeVariants.LightPurple),
        new("dark-red", "Dark Red", MissionPlannerThemeVariants.DarkRed),
        new("light-red", "Light Red", MissionPlannerThemeVariants.LightRed),
        new("dark-pink", "Dark Pink", MissionPlannerThemeVariants.DarkPink),
        new("light-pink", "Light Pink", MissionPlannerThemeVariants.LightPink),
        new("dark-violet", "Dark Violet", MissionPlannerThemeVariants.DarkViolet),
        new("light-violet", "Light Violet", MissionPlannerThemeVariants.LightViolet),
        new("dark-indigo", "Dark Indigo", MissionPlannerThemeVariants.DarkIndigo),
        new("light-indigo", "Light Indigo", MissionPlannerThemeVariants.LightIndigo),
        new("dark-yellow", "Dark Yellow", MissionPlannerThemeVariants.DarkYellow),
        new("light-yellow", "Light Yellow", MissionPlannerThemeVariants.LightYellow),
        new("dark-lime", "Dark Lime", MissionPlannerThemeVariants.DarkLime),
        new("light-lime", "Light Lime", MissionPlannerThemeVariants.LightLime),
        new("dark-light-green", "Dark Light Green", MissionPlannerThemeVariants.DarkLightGreen),
        new("light-light-green", "Light Light Green", MissionPlannerThemeVariants.LightLightGreen),
        new("dark-light-blue", "Dark Light Blue", MissionPlannerThemeVariants.DarkLightBlue),
        new("light-light-blue", "Light Light Blue", MissionPlannerThemeVariants.LightLightBlue),
        new("dark-teal", "Dark Teal", MissionPlannerThemeVariants.DarkTeal),
        new("light-teal", "Light Teal", MissionPlannerThemeVariants.LightTeal),
        new("dark-cyan", "Dark Cyan", MissionPlannerThemeVariants.DarkCyan),
        new("light-cyan", "Light Cyan", MissionPlannerThemeVariants.LightCyan),
        new("dark-green", "Dark Green", MissionPlannerThemeVariants.DarkGreen),
        new("light-green", "Light Green", MissionPlannerThemeVariants.LightGreen),
        new("dark-grey", "Dark Grey", MissionPlannerThemeVariants.DarkGrey),
        new("light-grey", "Light Grey", MissionPlannerThemeVariants.LightGrey),
        new("dark-ai-purple", "Dark AI Purple", MissionPlannerThemeVariants.DarkAIPurple),
        new("light-ai-purple", "Light AI Purple", MissionPlannerThemeVariants.LightAIPurple),
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
            var customizer = Colors;
            customizer.Restore();
            application.RequestedThemeVariant = theme.Theme;
            customizer.Apply(theme.Id);
        }

        ThemeChanged?.Invoke(null, theme);
    }
}

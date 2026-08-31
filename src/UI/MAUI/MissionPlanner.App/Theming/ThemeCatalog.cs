namespace MissionPlanner.App.Theming;

/// <summary>Provides the built-in MissionPlanner theme catalog.</summary>
public sealed class ThemeCatalog : IThemeCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<ThemeDescriptor> ConcreteThemes { get; } =
    [
        new(
            ThemeIds.MissionLight,
            "Mission Light",
            ThemeBaseAppearance.Light,
            "Resources/Themes/MissionLight.xaml"),
        new(
            ThemeIds.MissionDark,
            "Mission Dark",
            ThemeBaseAppearance.Dark,
            "Resources/Themes/MissionDark.xaml"),
        new(
            ThemeIds.MissionBlue,
            "Mission Blue",
            ThemeBaseAppearance.Light,
            "Resources/Themes/MissionBlue.xaml")
    ];

    /// <inheritdoc />
    public IReadOnlyList<ThemeOption> Options { get; } =
    [
        new(ThemeIds.System, "System"),
        new(ThemeIds.MissionLight, "Mission Light"),
        new(ThemeIds.MissionDark, "Mission Dark"),
        new(ThemeIds.MissionBlue, "Mission Blue")
    ];

    /// <inheritdoc />
    public bool TryGetTheme(string id, out ThemeDescriptor? theme)
    {
        theme = ConcreteThemes.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
        return theme is not null;
    }
}

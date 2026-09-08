namespace MissionPlanner.App.Theming;

/// <summary>Loads compiled MAUI theme dictionaries from application resources.</summary>
public sealed class XamlThemePaletteLoader : IThemePaletteLoader
{
    /// <inheritdoc />
    public ResourceDictionary Load(ThemeDescriptor theme)
    {
        return theme.Id switch
        {
            ThemeIds.MissionLight => new Resources.Themes.MissionLightPalette(),
            ThemeIds.MissionDark => new Resources.Themes.MissionDarkPalette(),
            ThemeIds.MissionBlue => new Resources.Themes.MissionBluePalette(),
            _ => throw new ArgumentException($"Theme '{theme.Id}' has no compiled palette.", nameof(theme))
        };
    }
}

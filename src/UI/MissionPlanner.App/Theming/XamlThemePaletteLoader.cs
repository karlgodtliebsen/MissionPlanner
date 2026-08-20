namespace MissionPlanner.App.Theming;

/// <summary>Loads compiled MAUI theme dictionaries from application resources.</summary>
public sealed class XamlThemePaletteLoader : IThemePaletteLoader
{
    /// <inheritdoc />
    public ResourceDictionary Load(ThemeDescriptor theme)
    {
        return new ResourceDictionary
        {
            Source = new Uri(theme.ResourcePath, UriKind.Relative)
        };
    }
}

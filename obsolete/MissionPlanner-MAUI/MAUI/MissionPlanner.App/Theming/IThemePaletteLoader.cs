namespace MissionPlanner.App.Theming;

/// <summary>Loads concrete theme resource dictionaries.</summary>
public interface IThemePaletteLoader
{
    /// <summary>Loads the resource dictionary described by a theme.</summary>
    /// <param name="theme">The concrete theme descriptor.</param>
    /// <returns>The loaded palette.</returns>
    ResourceDictionary Load(ThemeDescriptor theme);
}

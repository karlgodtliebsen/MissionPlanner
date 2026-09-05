namespace MissionPlanner.App.Theming;

/// <summary>Provides installed concrete themes and user-selectable theme policies.</summary>
public interface IThemeCatalog
{
    /// <summary>Gets installed concrete themes in display order.</summary>
    IReadOnlyList<ThemeDescriptor> ConcreteThemes { get; }

    /// <summary>Gets selectable policies and themes in display order.</summary>
    IReadOnlyList<ThemeOption> Options { get; }

    /// <summary>Finds a concrete theme by stable identifier.</summary>
    /// <param name="id">The theme identifier.</param>
    /// <param name="theme">The matching descriptor when found.</param>
    /// <returns>True when a concrete theme was found.</returns>
    bool TryGetTheme(string id, out ThemeDescriptor? theme);
}

namespace MissionPlanner.App.Theming;

/// <summary>Resolves, validates, and applies application themes.</summary>
public interface IThemeManager : IDisposable
{
    /// <summary>Gets user-selectable theme options.</summary>
    IReadOnlyList<ThemeOption> AvailableThemes { get; }

    /// <summary>Gets the selected theme or policy identifier.</summary>
    string SelectedThemeId { get; }

    /// <summary>Gets the concrete theme currently applied.</summary>
    ThemeDescriptor ActiveTheme { get; }

    /// <summary>Occurs once after a validated palette has been applied.</summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>Connects the manager to the application's active semantic dictionary.</summary>
    /// <param name="activeResources">The named active color dictionary.</param>
    void Initialize(ResourceDictionary activeResources);

    /// <summary>Applies and selects a theme or selection policy.</summary>
    Task ApplyAsync(string themeId, CancellationToken cancellationToken = default);

    /// <summary>Applies a theme for preview without changing the selected identifier.</summary>
    Task PreviewAsync(string themeId, CancellationToken cancellationToken = default);
}

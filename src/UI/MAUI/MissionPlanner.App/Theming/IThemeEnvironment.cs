namespace MissionPlanner.App.Theming;

/// <summary>
/// Provides the operating-system appearance boundary used by the theme manager.
/// </summary>
public interface IThemeEnvironment : IDisposable
{
    /// <summary>Gets the current operating-system requested appearance.</summary>
    AppTheme RequestedTheme { get; }

    /// <summary>Occurs when the operating-system requested appearance changes.</summary>
    event EventHandler<AppTheme>? RequestedThemeChanged;

    /// <summary>Attaches to the current MAUI application.</summary>
    void Attach();

    /// <summary>Sets the native-control fallback appearance.</summary>
    /// <param name="theme">The native appearance.</param>
    void SetUserTheme(AppTheme theme);
}

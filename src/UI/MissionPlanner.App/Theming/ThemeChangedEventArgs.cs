namespace MissionPlanner.App.Theming;

/// <summary>Provides details about a completed application theme change.</summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    /// <summary>Initializes theme change details.</summary>
    public ThemeChangedEventArgs(string selectedThemeId, ThemeDescriptor activeTheme)
    {
        SelectedThemeId = selectedThemeId;
        ActiveTheme = activeTheme;
    }

    /// <summary>Gets the selected policy or concrete theme identifier.</summary>
    public string SelectedThemeId { get; }

    /// <summary>Gets the concrete theme currently applied.</summary>
    public ThemeDescriptor ActiveTheme { get; }
}

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures application appearance.</summary>
public sealed record PlannerAppearanceSettings
{
    /// <summary>
    /// Application preferences that are not persisted in the settings file, but are used to control the UI and behavior of the application.
    /// </summary>
    public bool PreferDarkTheme { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is currently presented in the UI.
    /// </summary>
    public bool IsFlyoutPresented { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tutorial is currently presented in the UI.
    /// </summary>
    public bool IsTutorialPresented { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is locked in the UI.
    /// </summary>
    public bool IsFlyoutLocked { get; set; }


    /// <summary>
    /// Gets the application theme.
    /// </summary>
    public PlannerTheme Theme { get; init; } = PlannerTheme.System;
}

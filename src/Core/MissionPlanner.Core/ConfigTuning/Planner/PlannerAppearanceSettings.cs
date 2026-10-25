namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures application appearance.</summary>
public sealed record PlannerAppearanceSettings
{
    /// <summary>The default persisted application theme selection policy.</summary>
    public const string DefaultThemeId = "system";

    /// <summary>Gets the stable persisted theme or selection-policy identifier.</summary>
    public string ThemeId { get; init; } = DefaultThemeId;

    /// <summary>Gets per-theme, semantic color overrides encoded as #AARRGGBB.</summary>
    public Dictionary<string, Dictionary<string, string>> ColorOverrides { get; init; } = new();

    /// <summary>Gets or sets whether the legacy navigation flyout is shown at startup.</summary>
    public bool IsFlyoutVisibleAtStartup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tutorial is currently presented in the UI.
    /// </summary>
    public bool IsTutorialVisibleAtStartup
    {
        get; set;
    }

    /// <summary>Gets or sets whether the legacy navigation flyout remains locked open.</summary>
    public bool IsFlyoutLocked { get; set; }

}

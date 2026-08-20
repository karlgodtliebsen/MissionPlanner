namespace MissionPlanner.App.Theming;

/// <summary>Defines the semantic color resources that every concrete application theme must provide.</summary>
public static class ThemeResourceKeys
{
    /// <summary>Gets all required semantic color keys.</summary>
    public static IReadOnlyList<string> RequiredColorKeys { get; } =
    [
        Primary,
        OnPrimary,
        PrimaryContainer,
        OnPrimaryContainer,
        Secondary,
        OnSecondary,
        SecondaryContainer,
        OnSecondaryContainer,
        Tertiary,
        OnTertiary,
        TertiaryContainer,
        OnTertiaryContainer,
        Surface,
        OnSurface,
        SurfaceVariant,
        OnSurfaceVariant,
        SurfaceContainerLow,
        SurfaceContainer,
        SurfaceContainerHigh,
        Background,
        OnBackground,
        Success,
        OnSuccess,
        SuccessContainer,
        OnSuccessContainer,
        Warning,
        OnWarning,
        WarningContainer,
        OnWarningContainer,
        Info,
        OnInfo,
        InfoContainer,
        OnInfoContainer,
        Error,
        OnError,
        ErrorContainer,
        OnErrorContainer,
        Outline,
        OutlineVariant,
        Shadow,
        Scrim,
        InverseSurface,
        InverseOnSurface,
        InversePrimary,
        DisabledText,
        DisabledBackground
    ];

    /// <summary>Primary application and action accent.</summary>
    public const string Primary = nameof(Primary);

    /// <summary>Content drawn on the primary accent.</summary>
    public const string OnPrimary = nameof(OnPrimary);

    /// <summary>Low-emphasis primary container.</summary>
    public const string PrimaryContainer = nameof(PrimaryContainer);

    /// <summary>Content drawn on the primary container.</summary>
    public const string OnPrimaryContainer = nameof(OnPrimaryContainer);

    /// <summary>Secondary application accent.</summary>
    public const string Secondary = nameof(Secondary);

    /// <summary>Content drawn on the secondary accent.</summary>
    public const string OnSecondary = nameof(OnSecondary);

    /// <summary>Low-emphasis secondary container.</summary>
    public const string SecondaryContainer = nameof(SecondaryContainer);

    /// <summary>Content drawn on the secondary container.</summary>
    public const string OnSecondaryContainer = nameof(OnSecondaryContainer);

    /// <summary>Tertiary application accent.</summary>
    public const string Tertiary = nameof(Tertiary);

    /// <summary>Content drawn on the tertiary accent.</summary>
    public const string OnTertiary = nameof(OnTertiary);

    /// <summary>Low-emphasis tertiary container.</summary>
    public const string TertiaryContainer = nameof(TertiaryContainer);

    /// <summary>Content drawn on the tertiary container.</summary>
    public const string OnTertiaryContainer = nameof(OnTertiaryContainer);

    /// <summary>Default application surface.</summary>
    public const string Surface = nameof(Surface);

    /// <summary>Content drawn on a default surface.</summary>
    public const string OnSurface = nameof(OnSurface);

    /// <summary>Contrasting surface variant.</summary>
    public const string SurfaceVariant = nameof(SurfaceVariant);

    /// <summary>Content drawn on a surface variant.</summary>
    public const string OnSurfaceVariant = nameof(OnSurfaceVariant);

    /// <summary>Lowest elevated surface container.</summary>
    public const string SurfaceContainerLow = nameof(SurfaceContainerLow);

    /// <summary>Standard elevated surface container.</summary>
    public const string SurfaceContainer = nameof(SurfaceContainer);

    /// <summary>Highest elevated surface container.</summary>
    public const string SurfaceContainerHigh = nameof(SurfaceContainerHigh);

    /// <summary>Application background.</summary>
    public const string Background = nameof(Background);

    /// <summary>Content drawn on the application background.</summary>
    public const string OnBackground = nameof(OnBackground);

    /// <summary>Successful operational state.</summary>
    public const string Success = nameof(Success);

    /// <summary>Content drawn on a success color.</summary>
    public const string OnSuccess = nameof(OnSuccess);

    /// <summary>Low-emphasis success container.</summary>
    public const string SuccessContainer = nameof(SuccessContainer);

    /// <summary>Content drawn on a success container.</summary>
    public const string OnSuccessContainer = nameof(OnSuccessContainer);

    /// <summary>Warning or caution state.</summary>
    public const string Warning = nameof(Warning);

    /// <summary>Content drawn on a warning color.</summary>
    public const string OnWarning = nameof(OnWarning);

    /// <summary>Low-emphasis warning container.</summary>
    public const string WarningContainer = nameof(WarningContainer);

    /// <summary>Content drawn on a warning container.</summary>
    public const string OnWarningContainer = nameof(OnWarningContainer);

    /// <summary>Informational operational state.</summary>
    public const string Info = nameof(Info);

    /// <summary>Content drawn on an information color.</summary>
    public const string OnInfo = nameof(OnInfo);

    /// <summary>Low-emphasis information container.</summary>
    public const string InfoContainer = nameof(InfoContainer);

    /// <summary>Content drawn on an information container.</summary>
    public const string OnInfoContainer = nameof(OnInfoContainer);

    /// <summary>Error or critical operational state.</summary>
    public const string Error = nameof(Error);

    /// <summary>Content drawn on an error color.</summary>
    public const string OnError = nameof(OnError);

    /// <summary>Low-emphasis error container.</summary>
    public const string ErrorContainer = nameof(ErrorContainer);

    /// <summary>Content drawn on an error container.</summary>
    public const string OnErrorContainer = nameof(OnErrorContainer);

    /// <summary>High-emphasis outline.</summary>
    public const string Outline = nameof(Outline);

    /// <summary>Low-emphasis outline.</summary>
    public const string OutlineVariant = nameof(OutlineVariant);

    /// <summary>Elevation shadow color.</summary>
    public const string Shadow = nameof(Shadow);

    /// <summary>Modal scrim color.</summary>
    public const string Scrim = nameof(Scrim);

    /// <summary>Inverse surface used for contrasting transient UI.</summary>
    public const string InverseSurface = nameof(InverseSurface);

    /// <summary>Content drawn on an inverse surface.</summary>
    public const string InverseOnSurface = nameof(InverseOnSurface);

    /// <summary>Primary accent drawn on an inverse surface.</summary>
    public const string InversePrimary = nameof(InversePrimary);

    /// <summary>Disabled content color.</summary>
    public const string DisabledText = nameof(DisabledText);

    /// <summary>Disabled control background.</summary>
    public const string DisabledBackground = nameof(DisabledBackground);
}

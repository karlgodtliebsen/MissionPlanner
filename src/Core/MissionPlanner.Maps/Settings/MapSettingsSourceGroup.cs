namespace MissionPlanner.Maps.Settings;

/// <summary>Identifies the user-facing group in which a map source is displayed.</summary>
public enum MapSettingsSourceGroup
{
    /// <summary>An installed offline pack.</summary>
    OfflinePacks,

    /// <summary>A source controlled by the operator.</summary>
    SelfHostedOrCustom,

    /// <summary>A hosted online provider.</summary>
    OnlineProviders,

    /// <summary>The intentionally blank basemap.</summary>
    BlankMap
}

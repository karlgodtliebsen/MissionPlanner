namespace MissionPlanner.Maps.Attribution;

/// <summary>View-independent state for the standard compact or expanded attribution overlay.</summary>
public sealed class MapAttributionOverlayState
{
    /// <summary>Gets or sets whether expanded attribution is shown.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Gets the text appropriate for the current mode.</summary>
    /// <param name="snapshot">Current attribution.</param>
    /// <returns>Compact or expanded display text.</returns>
    public string GetDisplayText(MapAttributionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return IsExpanded ? snapshot.ExpandedText : snapshot.CompactText;
    }
}

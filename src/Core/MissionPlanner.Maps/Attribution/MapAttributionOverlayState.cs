namespace MissionPlanner.Maps.Attribution;

/// <summary>View-independent state for the standard compact or expanded attribution overlay.</summary>
public sealed class MapAttributionOverlayState
{
    /// <summary>Initializes an empty overlay state.</summary>
    public MapAttributionOverlayState() : this(new MapAttributionSnapshot([])) { }

    /// <summary>Initializes state for a snapshot.</summary>
    public MapAttributionOverlayState(MapAttributionSnapshot snapshot, bool isExpanded = false, bool isDegraded = false)
    {
        Snapshot = snapshot;
        IsExpanded = isExpanded;
        IsDegraded = isDegraded;
    }

    /// <summary>Gets the current exportable attribution snapshot.</summary>
    public MapAttributionSnapshot Snapshot { get; }

    /// <summary>Gets or sets whether expanded attribution is shown.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Gets whether dynamic metadata used a reviewed fallback.</summary>
    public bool IsDegraded { get; }

    /// <summary>Gets the text appropriate for the current mode.</summary>
    public string DisplayText => GetDisplayText(Snapshot);

    /// <summary>Gets the text appropriate for the current mode.</summary>
    /// <param name="snapshot">Current attribution.</param>
    /// <returns>Compact or expanded display text.</returns>
    public string GetDisplayText(MapAttributionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return IsExpanded ? snapshot.ExpandedText : snapshot.CompactText;
    }
}

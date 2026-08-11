using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Attribution;

/// <summary>Contains deduplicated attribution for display and export.</summary>
/// <param name="Entries">All current attribution entries.</param>
public sealed record MapAttributionSnapshot(IReadOnlyList<MapAttributionEntry> Entries)
{
    /// <summary>Gets entries required on the interactive map.</summary>
    public IReadOnlyList<MapAttributionEntry> OnMap => Entries.Where(item => item.RequiredOnMap).ToArray();

    /// <summary>Gets entries required in exported output.</summary>
    public IReadOnlyList<MapAttributionEntry> OnExport => Entries.Where(item => item.RequiredOnExport).ToArray();

    /// <summary>Gets compact display text.</summary>
    public string CompactText => string.Join(" · ", OnMap.Select(item => item.Text));

    /// <summary>Gets expanded display text.</summary>
    public string ExpandedText => string.Join(Environment.NewLine, OnMap.Select(item => item.Text));
}

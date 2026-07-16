using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Display/edit row for a mission item in the mission list. The value fields are strings so the
/// complete editor can bind them to entries; Command and Frame are select values (v1.38-style
/// names). Edits are applied back to the mission via
/// <see cref="MissionMapViewModel.ApplyRowEditCommand"/>; command/frame selection changes apply
/// immediately through the attached selection callback.
/// </summary>
public sealed partial class MissionItemRow : ObservableObject
{
    private Action<MissionItemRow>? selectionChanged;

    /// <summary>The identifier of the underlying mission item.</summary>
    public required MissionItemId Id { get; init; }

    /// <summary>The 1-based display number (sequence + 1).</summary>
    public required int Number { get; init; }

    /// <summary>The MAVLink command id (MAV_CMD) the row was built from (fallback when the selection is unknown).</summary>
    public required ushort CommandId { get; init; }

    /// <summary>The MAVLink frame byte the row was built from (fallback when the selection is unknown).</summary>
    public required byte Frame { get; init; }

    /// <summary>Whether the item auto-continues to the next one.</summary>
    public required bool AutoContinue { get; init; }

    /// <summary>The selected command name (v1.38 mavcmd naming, e.g. WAYPOINT, LOITER_TIME).</summary>
    [ObservableProperty]
    public partial string? SelectedCommand { get; set; } = "WAYPOINT";

    /// <summary>The selected altitude frame name (Absolute, Relative, Terrain).</summary>
    [ObservableProperty]
    public partial string? SelectedFrame { get; set; } = "Relative";

    /// <summary>Command parameter 1 (editable).</summary>
    public string Param1 { get; set; } = string.Empty;

    /// <summary>Command parameter 2 (editable).</summary>
    public string Param2 { get; set; } = string.Empty;

    /// <summary>Command parameter 3 (editable).</summary>
    public string Param3 { get; set; } = string.Empty;

    /// <summary>Command parameter 4 (editable).</summary>
    public string Param4 { get; set; } = string.Empty;

    /// <summary>The latitude in degrees (editable), or empty when the item has no position.</summary>
    public string Latitude { get; set; } = string.Empty;

    /// <summary>The longitude in degrees (editable), or empty when the item has no position.</summary>
    public string Longitude { get; set; } = string.Empty;

    /// <summary>The altitude in meters (editable), or empty when the item has no altitude.</summary>
    public string Altitude { get; set; } = string.Empty;

    /// <summary>Ground distance in meters from the previous positioned item (or home), display only.</summary>
    public string Distance { get; init; } = string.Empty;

    /// <summary>Bearing in degrees from the previous positioned item (or home), display only.</summary>
    public string Azimuth { get; init; } = string.Empty;

    /// <summary>Climb gradient in percent over the leg, display only.</summary>
    public string Gradient { get; init; } = string.Empty;

    /// <summary>
    /// Attaches the callback invoked when the command or frame selection changes. Attach after
    /// setting the initial selections so building the row does not trigger an apply.
    /// </summary>
    public void AttachSelectionChanged(Action<MissionItemRow> callback)
    {
        selectionChanged = callback;
    }

    partial void OnSelectedCommandChanged(string? oldValue, string? newValue)
    {
        if (oldValue is not null && newValue is not null && oldValue != newValue)
        {
            selectionChanged?.Invoke(this);
        }
    }

    partial void OnSelectedFrameChanged(string? oldValue, string? newValue)
    {
        if (oldValue is not null && newValue is not null && oldValue != newValue)
        {
            selectionChanged?.Invoke(this);
        }
    }
}

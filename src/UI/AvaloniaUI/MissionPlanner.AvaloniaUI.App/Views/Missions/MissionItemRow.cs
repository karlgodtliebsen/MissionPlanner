using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>
/// Display/edit row for a mission item in the mission list. The value fields are strings so the
/// complete editor can bind them to entries; Command and Frame are select values (v1.38-style
/// names). Edits are applied back to the mission via
/// immediately through the attached selection callback.
/// </summary>
public sealed partial class MissionItemRow : ObservableObject, IDisposable
{
    private bool Equals(MissionItemRow other)
    {
        return Id.Equals(other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is MissionItemRow other && Equals(other));
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    private Action<MissionItemRow>? selectionChanged;
    private Action<MissionItemRow>? valueChanged;

    /// <summary>The identifier of the underlying mission item.</summary>
    public required MissionItemId Id
    {
        get; init;
    }

    /// <summary>The 1-based display number (sequence + 1).</summary>
    public required int Number
    {
        get; init;
    }

    /// <summary>The MAVLink command id (MAV_CMD) the row was built from (fallback when the selection is unknown).</summary>
    public required ushort CommandId
    {
        get; init;
    }

    /// <summary>The MAVLink frame byte the row was built from (fallback when the selection is unknown).</summary>
    public required byte Frame
    {
        get; init;
    }

    /// <summary>Whether the item auto-continues to the next one.</summary>
    public required bool AutoContinue
    {
        get; init;
    }

    /// <summary>The selected command name (v1.38 mavcmd naming, e.g. WAYPOINT, LOITER_TIME).</summary>
    [ObservableProperty]
    public partial string? SelectedCommand { get; set; } = "WAYPOINT";

    /// <summary>The selected altitude frame name (Absolute, Relative, Terrain).</summary>
    [ObservableProperty]
    public partial string? SelectedFrame { get; set; } = "Relative";

    /// <summary>Command parameter 1 (editable).</summary>
    [ObservableProperty]
    public partial string Param1 { get; set; } = string.Empty;

    /// <summary>Command parameter 2 (editable).</summary>
    [ObservableProperty]
    public partial string Param2 { get; set; } = string.Empty;

    /// <summary>Command parameter 3 (editable).</summary>
    [ObservableProperty]
    public partial string Param3 { get; set; } = string.Empty;

    /// <summary>Command parameter 4 (editable).</summary>
    [ObservableProperty]
    public partial string Param4 { get; set; } = string.Empty;

    /// <summary>The latitude in degrees (editable), or empty when the item has no position.</summary>
    [ObservableProperty]
    public partial double? Latitude
    {
        get; set;
    }

    /// <summary>The longitude in degrees (editable), or empty when the item has no position.</summary>
    [ObservableProperty]
    public partial double? Longitude
    {
        get; set;
    }

    /// <summary>The altitude in meters (editable), or empty when the item has no altitude.</summary>
    [ObservableProperty]
    public partial double? Altitude
    {
        get; set;
    }

    /// <summary>Ground distance in meters from the previous positioned item (or home), display only.</summary>
    public double? Distance
    {
        get; init;
    }

    /// <summary>Bearing in degrees from the previous positioned item (or home), display only.</summary>
    public double? Azimuth
    {
        get; init;
    }

    /// <summary>Climb gradient in percent over the leg, display only.</summary>
    public double? Gradient
    {
        get; init;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not nameof(SelectedFrame) and not nameof(SelectedCommand))
        {
            valueChanged?.Invoke(this);
        }
    }

    partial void OnLatitudeChanged(double? value)
    {
        LatitudeChanged?.Invoke(value, Id);
    }

    partial void OnAltitudeChanged(double? value)
    {
        AltitudeChanged?.Invoke(value, Id);
    }

    partial void OnLongitudeChanged(double? value)
    {
        LongitudeChanged?.Invoke(value, Id);
    }

    public event Action<double?, MissionItemId>? LatitudeChanged;

    public event Action<double?, MissionItemId>? LongitudeChanged;

    public event Action<double?, MissionItemId>? AltitudeChanged;



    partial void OnSelectedCommandChanged(string? oldValue, string? newValue)
    {
        // A picker clear (or recycle race) pushes null: restore the previous value so the
        // select never sits without a selection. The restore itself does not apply an edit.
        if (newValue is null && oldValue is not null)
        {
            SelectedCommand = oldValue;
            return;
        }

        if (oldValue is not null && newValue is not null && oldValue != newValue)
        {
            selectionChanged?.Invoke(this);
        }
    }

    partial void OnSelectedFrameChanged(string? oldValue, string? newValue)
    {
        if (newValue is null && oldValue is not null)
        {
            SelectedFrame = oldValue;
            return;
        }

        if (oldValue is not null && newValue is not null && oldValue != newValue)
        {
            selectionChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Callback to owner viewModel to apply a command/frame selection change immediately to the underlying mission item.
    /// </summary>
    /// <param name="sChanged"></param>
    /// <param name="vChanged"></param>
    public void AttachNotifications(Action<MissionItemRow> sChanged, Action<MissionItemRow> vChanged)
    {
        selectionChanged = sChanged;
        valueChanged = vChanged;
    }


    /// <inheritdoc />
    public void Dispose()
    {
        AltitudeChanged = null;
        LongitudeChanged = null;
        LatitudeChanged = null;
        selectionChanged = null;
        valueChanged = null;
    }
}

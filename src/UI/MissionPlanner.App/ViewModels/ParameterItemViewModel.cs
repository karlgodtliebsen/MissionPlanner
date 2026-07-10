using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Material.Controls;

namespace MissionPlanner.App.ViewModels;

/// <summary>
/// View model for a single parameter item in the grid.
/// Wraps VehicleParameter with additional UI properties.
/// </summary>
public partial class ParameterItemViewModel : ObservableObject
{
    private readonly VehicleParameter originalParameter;

    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial float Value { get; set; }

    [DisplayName("Default")][ObservableProperty] public partial float OriginalValue { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial string? Units { get; set; }

    [DataGridIgnore][ObservableProperty] public partial string Type { get; set; } = string.Empty;
    [DataGridIgnore][ObservableProperty] public partial ushort Index { get; set; }
    [DataGridIgnore][ObservableProperty] public partial float? Min { get; set; }
    [DataGridIgnore][ObservableProperty] public partial float? Max { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? Range { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? Values { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsModified { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsReadOnly { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool Favorite { get; set; }

    public ParameterItemViewModel(VehicleParameter parameter)
    {
        originalParameter = parameter;
        Name = parameter.Name;
        Value = parameter.Value;
        OriginalValue = parameter.Value;
        Type = parameter.Type.ToString();
        Index = parameter.Index;
    }

    /// <summary>
    /// Sets metadata for this parameter.
    /// </summary>
    public void SetMetadata(ParameterMetadata? metadata)
    {
        if (metadata == null)
        {
            return;
        }

        Description = metadata.Description;
        Units = metadata.Units;
        Min = metadata.MinValue;
        Max = metadata.MaxValue;
        Range = metadata.Range;
        Values = metadata.Values;
        IsReadOnly = metadata.ReadOnly;
    }

    /// <summary>
    /// Checks if the value has been modified from the original.
    /// </summary>
    partial void OnValueChanged(float value)
    {
        IsModified = Math.Abs(value - OriginalValue) > 0.0001f;
    }

    /// <summary>
    /// Resets the value to the original vehicle value.
    /// </summary>
    public void ResetToOriginal()
    {
        Value = OriginalValue;
    }

    /// <summary>
    /// Gets the original VehicleParameter for sending to the vehicle.
    /// </summary>
    public VehicleParameter ToVehicleParameter()
    {
        return new VehicleParameter(
            Name,
            Value,
            originalParameter.Type,
            Index,
            originalParameter.Count);
    }
}

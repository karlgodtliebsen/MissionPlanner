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
    //private readonly VehicleParameter originalParameter;

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial float Value { get; set; }

    [DisplayName("Default")][ObservableProperty] public partial float OriginalValue { get; set; }
    [ObservableProperty] public partial string? Units { get; set; }

    [ObservableProperty] public partial string? Options { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }

    [DataGridIgnore][ObservableProperty] public partial VehicleParameter OriginalParameter { get; set; }

    //[DataGridIgnore][ObservableProperty] public partial string Type { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial ushort Index { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial float? Min { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial float? Max { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial string? Range { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial string? Values { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial bool IsModified { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial bool IsReadOnly { get; set; }
    //[DataGridIgnore][ObservableProperty] public partial bool Favorite { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterItemViewModel"/> class.
    /// </summary>
    /// <param name="parameter">The vehicle parameter to wrap.</param>
    public ParameterItemViewModel(VehicleParameter parameter)
    {
        OriginalParameter = parameter;
        Name = parameter.Name;
        Value = parameter.Value;
        OriginalValue = parameter.Value;
        //Type = parameter.Type.ToString();
        //Index = parameter.Index;
    }

    public ParameterItemViewModel()
    {
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
        //Range = metadata.Range;
        //Min = metadata.MinValue;
        //Max = metadata.MaxValue;
        //Values = metadata.Values;
        //IsReadOnly = metadata.ReadOnly;
    }

    /// <summary>
    /// Checks if the value has been modified from the original.
    /// </summary>
    partial void OnValueChanged(float value)
    {
        //IsModified = Math.Abs(value - OriginalValue) > 0.0001f;
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
            OriginalParameter.Type,
            OriginalParameter.Index,
            OriginalParameter.Count
        );
    }
}

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
    private VehicleParameter? originalParameter;
    private ParameterMetadata? originalMetadata;
    public VehicleParameter? OriginalParameter => originalParameter;

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string? DisplayName { get; set; }
    [ObservableProperty] public partial float Value { get; set; }

    [DisplayName("Default")][ObservableProperty] public partial float OriginalValue { get; set; }
    [ObservableProperty] public partial string? Units { get; set; }
    [ObservableProperty] public partial string? UnitText { get; set; }

    [ObservableProperty] public partial string? Values { get; set; }
    [ObservableProperty] public partial string? Range { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }

    [ObservableProperty] public partial string? Options { get; set; }
    [ObservableProperty] public partial string[]? ValuesData { get; set; }
    [ObservableProperty] public partial string[]? RangeData { get; set; }

    [DataGridIgnore][ObservableProperty] public partial string? Increment { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? UserLevel { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? Bitmask { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsModified { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsReadOnly { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool RebootRequired { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterItemViewModel"/> class.
    /// </summary>
    /// <param name="parameter">The vehicle parameter to wrap.</param>
    public ParameterItemViewModel(VehicleParameter parameter)
    {
        SetData(parameter);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterItemViewModel"/> class with metadata.
    /// </summary>
    /// <param name="metadata">The parameter metadata to wrap.</param>
    public ParameterItemViewModel(ParameterMetadata metadata)
    {
        SetMetadata(metadata);
    }


    /// <summary>
    /// Sets metadata for this parameter.
    /// </summary>
    public void SetMetadata(ParameterMetadata? metadata)
    {
        originalMetadata = metadata;
        Name = metadata.Name;
        DisplayName = metadata.DisplayName;

        //TODO: handle Range and values as Dictionary for better UI representation or add properties for data binding to Range/Values.

        Description = metadata.Description ?? "";
        Units = metadata.Units ?? "";
        UnitText = metadata.UnitText ?? "";
        Range = metadata.Range;
        Values = metadata.Values;
        Bitmask = metadata.Bitmask;
        Increment = metadata.Increment;
        UserLevel = metadata.UserLevel;
        RebootRequired = metadata.RebootRequired;
        IsReadOnly = metadata.ReadOnly;

        ValuesData = [];
        RangeData = [];
        var opt = "";
        if (Range is not null)
        {
            opt = Range + Environment.NewLine;
            RangeData = Range.Split(" ");
        }

        if (Values is not null)
        {
            var v = Values.Split(",");
            ValuesData = v;
            Options = opt + string.Join(Environment.NewLine, v);
        }
        // Options = opt + string.Join(Environment.NewLine, v);
    }

    /// <summary>
    /// Sets the data for this parameter from a VehicleParameter instance.
    /// </summary>
    /// <param name="parameter">The VehicleParameter instance containing the data.</param>
    public void SetData(VehicleParameter parameter)
    {
        originalParameter = parameter;
        OriginalValue = parameter.Value;
        Value = parameter.Value;
        Value = parameter.Value;
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
}

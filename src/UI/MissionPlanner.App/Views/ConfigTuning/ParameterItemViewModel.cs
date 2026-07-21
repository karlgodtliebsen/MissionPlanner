using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Material.Controls;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>
/// View model for a single parameter item in the grid.
/// Wraps VehicleParameter with additional UI properties.
/// </summary>
public partial class ParameterItemViewModel : ObservableObject
{
    private VehicleParameter? originalParameter;
    private ParameterMetadata? originalMetadata;

    private bool loadingData;

    //private float[]? numericRange = [];
    private float stepSize = 0.0f;

    /// <summary>
    /// Provides the public API for OriginalParameter.
    /// </summary>
    public VehicleParameter? OriginalParameter => originalParameter;

    [ObservableProperty] public partial string Name { get; set; } = null!;
    [ObservableProperty] public partial string? DisplayName { get; set; }
    [ObservableProperty] public partial float Value { get; set; }
    [ObservableProperty] public partial string? SelectedValue { get; set; }

    [DisplayName("Default")][ObservableProperty] public partial float OriginalValue { get; set; }

    [ObservableProperty] public partial float Max { get; set; }
    [ObservableProperty] public partial float Min { get; set; }

    [ObservableProperty] public partial string? Units { get; set; }
    [ObservableProperty] public partial string? UnitText { get; set; }

    [ObservableProperty] public partial string? Values { get; set; }
    [ObservableProperty] public partial string? Range { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }

    [ObservableProperty] public partial string? Options { get; set; }
    [ObservableProperty] public partial string[]? ValuesData { get; set; }
    [ObservableProperty] public partial SelectItem[] ValuesItems { get; set; } = [];
    [ObservableProperty] public partial string[]? RangeData { get; set; }

    [DataGridIgnore][ObservableProperty] public partial string? Increment { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? UserLevel { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? Bitmask { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsModified { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsReadOnly { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool RebootRequired { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasValuesData { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasNumericRangeData { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsSimpleValue { get; set; }

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

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Value))
        {
            //  EnsureNumericValueValid();
        }
    }


    /// <summary>
    /// Sets metadata for this parameter.
    /// </summary>
    public void SetMetadata(ParameterMetadata metadata)
    {
        //Value = 0.0f;
        loadingData = true;
        originalMetadata = metadata;
        ValuesData = [];
        RangeData = [];
        ValuesItems ??= [];
        var range = "";

        Name = metadata.Name;
        DisplayName = metadata.DisplayName;
        IsSimpleValue = true;
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
        Max = metadata.MaxValue ?? float.MaxValue;
        Min = metadata.MinValue ?? float.MinValue;

        if (Range is not null)
        {
            range = Range + Environment.NewLine;
            RangeData = Range.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            HasNumericRangeData = RangeData.Length == 2;
            stepSize = metadata.IncrementValue ?? 0.1f;
        }

        if (Values is not null)
        {
            var v = Values.Split(",");
            foreach (var s in v)
            {
                var x = s.Split(":");
                if (x.Length == 2)
                {
                    var value = float.Parse(x[0], NumberStyles.Float, CultureInfo.InvariantCulture);
                    var name = x[1];
                    ValuesItems = ValuesItems.Append(new SelectItem(name, value)).ToArray();
                    ValuesData = ValuesData.Append(name).ToArray();
                }
            }

            HasValuesData = true;
            HasNumericRangeData = false;
            IsSimpleValue = false;
            SelectedValue = ValuesItems.FirstOrDefault()?.Name;
            Options = range + string.Join(Environment.NewLine, v);
            IsReadOnly = ValuesItems.Length > 0;
        }

        loadingData = false;
    }

    [RelayCommand]
    private void IncrementNumber()
    {
        if (HasNumericRangeData)
        {
            if (Value + stepSize <= Max)
            {
                Value += stepSize;
            }
        }
    }

    private void EnsureNumericValueValid()
    {
        if (HasNumericRangeData && originalMetadata is not null)
        {
            //originalMetadata.IsValueValid(Value)
            if (Value > Max)
            {
                Value = Max;
            }

            if (Value < Min)
            {
                Value = Min;
            }
        }
    }

    [RelayCommand]
    private void DecrementNumber()
    {
        if (HasNumericRangeData)
        {
            if (Value - stepSize >= Min)
            {
                Value -= stepSize;
            }
        }
    }

    /// <summary>
    /// Sets the data for this parameter from a VehicleParameter instance.
    /// </summary>
    /// <param name="parameter">The VehicleParameter instance containing the data.</param>
    public void SetData(VehicleParameter parameter)
    {
        loadingData = true;
        originalParameter = parameter;
        OriginalValue = parameter.Value;
        Name = parameter.Name;
        Value = parameter.Value;
        var item = ValuesItems.FirstOrDefault(i => Math.Abs(i.Value - Value) < 0.0001f);
        if (item is not null && SelectedValue != item.Name)
        {
            SelectedValue = item.Name;
        }

        loadingData = false;
    }


    partial void OnSelectedValueChanged(string? oldValue, string? newValue)
    {
        if (loadingData)
        {
            return;
        }

        var item = ValuesItems.FirstOrDefault(i => i.Name == newValue);
        if (item is not null && Math.Abs(item.Value - Value) > 0.0001f)
        {
            Value = item.Value;
        }
    }


    /// <summary>
    /// Checks if the value has been modified from the original.
    /// </summary>
    partial void OnValueChanged(float value)
    {
        if (loadingData)
        {
            return;
        }

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

/// <summary>
/// Provides the public API for SelectItem.
/// </summary>
public sealed record SelectItem(string Name, float Value);

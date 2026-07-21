using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
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

    private float stepSize = 0.1f;

    /// <summary>
    /// Provides the public API for OriginalParameter.
    /// </summary>
    public VehicleParameter? OriginalParameter => originalParameter;

    /// <summary>
    /// Command that is triggered when the selected values change.
    /// </summary>
    public ICommand SelectedValuesChanged { get; }

    [DisplayName("Default")][ObservableProperty] public partial float OriginalValue { get; set; }

    [ObservableProperty] public partial string Name { get; set; } = null!;
    [ObservableProperty] public partial string? DisplayName { get; set; }
    [ObservableProperty] public partial float Value { get; set; }
    [ObservableProperty] public partial string? SelectedValue { get; set; }

    [ObservableProperty] public partial float Max { get; set; }
    [ObservableProperty] public partial float Min { get; set; }

    [ObservableProperty] public partial string? Units { get; set; }
    [ObservableProperty] public partial string? UnitText { get; set; }

    [ObservableProperty] public partial string? Values { get; set; }
    [ObservableProperty] public partial string? Range { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }

    [ObservableProperty] public partial string? Options { get; set; }
    [ObservableProperty] public partial string[]? ValuesData { get; set; }
    [ObservableProperty] public partial string[]? RangeData { get; set; }

    [ObservableProperty] public partial SelectItem[]? ValuesItems { get; set; }
    [ObservableProperty] public partial SelectItem[]? BitmaskOptions { get; set; }
    [ObservableProperty] public partial SelectItem? SelectedBitmaskItems { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? Increment { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? UserLevel { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? Bitmask { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsModified { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsReadOnly { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool RebootRequired { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasValuesData { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasNumericRangeData { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasBitmask { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterItemViewModel"/> class.
    /// </summary>
    /// <param name="parameter">The vehicle parameter to wrap.</param>
    public ParameterItemViewModel(VehicleParameter parameter)
    {
        SelectedValuesChanged = new Command<object>(OnSelectedValuesChanged);
        SetData(parameter);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterItemViewModel"/> class with metadata.
    /// </summary>
    /// <param name="metadata">The parameter metadata to wrap.</param>
    public ParameterItemViewModel(ParameterMetadata metadata)
    {
        SelectedValuesChanged = new Command<object>(OnSelectedValuesChanged);
        SetMetadata(metadata);
    }

    private void OnSelectedValuesChanged(object value)
    {
        Value = 0.0f;
        if (value is ObservableCollection<object> items)
        {
            foreach (var item in items)
            {
                var x = item as SelectItem;
                Value += x?.Value ?? 0.0f;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Value))
        {
        }
    }


    /// <summary>
    /// Sets metadata for this parameter.
    /// </summary>
    public void SetMetadata(ParameterMetadata metadata)
    {
        originalMetadata = metadata;
        Value = 0f;
        loadingData = true;
        ValuesData = [];
        RangeData = [];
        ValuesItems ??= [];

        Name = metadata.Name;
        DisplayName = metadata.DisplayName;
        Description = metadata.Description ?? "";
        Units = metadata.Units ?? "";
        UnitText = metadata.UnitText ?? "";
        Range = metadata.Range;
        Values = metadata.Values;
        Bitmask = metadata.Bitmask; //0:Air-mode,1:Rate Loop Only
        UserLevel = metadata.UserLevel;
        RebootRequired = metadata.RebootRequired;
        IsReadOnly = metadata.ReadOnly;
        Max = metadata.MaxValue ?? float.MaxValue;
        Min = metadata.MinValue ?? float.MinValue;
        Increment = metadata.Increment;
        if (Increment is not null)
        {
            stepSize = metadata.IncrementValue ?? 0.1f;
        }
        else if (metadata.MaxValue is not null && metadata.MinValue is not null)
        {
            stepSize = (float)Math.Round((Max - Min) / 10.0);
        }

        if (Range is not null)
        {
            RangeData = Range.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            HasNumericRangeData = RangeData.Length == 2;
        }

        if (Values is not null)
        {
            var vals = metadata.GetValueOptions();
            ValuesItems = vals.Keys.Select(k => new SelectItem(vals[k], k)).ToArray();
            ValuesData = vals.Values.ToArray();
            HasValuesData = true;
            HasNumericRangeData = false;
            SelectedValue = ValuesItems.FirstOrDefault()?.Name;
            Options = string.Join(Environment.NewLine, vals.Select(v => $"{v.Key}:{v.Value}"));
            //IsReadOnly = ValuesItems.Length > 0;
        }

        if (Bitmask is not null)
        {
            HasBitmask = true;
            var bitmaskOptions = metadata.GetBitmaskOptions();
            BitmaskOptions = bitmaskOptions.Keys.Select(k => new SelectItem(bitmaskOptions[k], k)).ToArray();
            SelectedBitmaskItems = null;
            Options = string.Join(Environment.NewLine, bitmaskOptions.Select(b => $"{b.Key}:{b.Value}"));
            IsReadOnly = true;
        }

        loadingData = false;
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
        if (!string.IsNullOrEmpty(parameter.Name))
        {
            Name = parameter.Name;
        }

        Value = parameter.Value;
        if (ValuesItems is not null)
        {
            var item = ValuesItems.FirstOrDefault(i => Math.Abs(i.Value - Value) < 0.0001f);
            if (item is not null && SelectedValue != item.Name)
            {
                SelectedValue = item.Name;
            }
        }

        if (BitmaskOptions is not null)
        {
            //TODO: The Value needs to be used to set SelectedBitmaskItems.
            //Ex. If Value is 1 and the BitMaskOptions is: //0:Air-mode,1:Rate Loop Only then selection must be set

            var item = BitmaskOptions.FirstOrDefault(i => Math.Abs(i.Value - Value) < 0.0001f);
        }


        loadingData = false;
    }

    [RelayCommand]
    private void IncrementNumber()
    {
        var v = Value;
        if (v + stepSize <= Max)
        {
            v += stepSize;
            Value = v;
        }
    }

    [RelayCommand]
    private void DecrementNumber()
    {
        var v = Value;

        if (v - stepSize >= Min)
        {
            v -= stepSize;
            Value = v;
        }
    }


    partial void OnSelectedValueChanged(string? oldValue, string? newValue)
    {
        if (loadingData)
        {
            return;
        }

        if (ValuesItems is not null)
        {
            var item = ValuesItems.FirstOrDefault(i => i.Name == newValue);
            if (item is not null && Math.Abs(item.Value - Value) > 0.0001f)
            {
                Value = item.Value;
            }
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
public sealed record SelectItem(string Name, float Value)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.ConfigTuning;
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
    private readonly IParameterEditSession? editSession;

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
    [ObservableProperty] public partial float LiveValue { get; set; }
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
    [ObservableProperty] public partial ObservableCollection<object> SelectedBitmaskItems { get; set; } = [];
    [DataGridIgnore][ObservableProperty] public partial string? Increment { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? UserLevel { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? Bitmask { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsModified { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool IsReadOnly { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool RebootRequired { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasValuesData { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasNumericRangeData { get; set; }
    [DataGridIgnore][ObservableProperty] public partial bool HasBitmask { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? ValidationError { get; set; }
    [DataGridIgnore][ObservableProperty] public partial ParameterEditWriteStatus WriteStatus { get; set; }
    [DataGridIgnore][ObservableProperty] public partial string? WriteMessage { get; set; }

    /// <summary>
    /// Initializes an empty parameter item for serializers and design-time tooling.
    /// </summary>
    public ParameterItemViewModel()
    {
        SelectedValuesChanged = new Command<object>(OnSelectedValuesChanged);
    }

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

    /// <summary>Initializes an item backed by the shared parameter editing session.</summary>
    /// <param name="editSession">The shared vehicle parameter editing session.</param>
    /// <param name="field">The initial field projection.</param>
    public ParameterItemViewModel(IParameterEditSession editSession, ParameterEditField field)
    {
        this.editSession = editSession;
        SelectedValuesChanged = new Command<object>(OnSelectedValuesChanged);
        SetField(field);
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
            SelectedBitmaskItems.Clear();
            Options = string.Join(Environment.NewLine, bitmaskOptions.Select(b => $"{b.Key}:{b.Value}"));
            IsReadOnly = true;
        }

        loadingData = false;
    }

    /// <summary>Updates this item from the shared editing-session field.</summary>
    /// <param name="field">The latest field projection.</param>
    public void SetField(ParameterEditField field)
    {
        loadingData = true;
        originalParameter = new VehicleParameter(field.Name, (float)field.OriginalValue, field.Type, 0, 0);
        originalMetadata = null;
        Name = field.Name;
        DisplayName = field.Metadata.DisplayName ?? field.Name;
        Description = field.Metadata.Description ?? string.Empty;
        Units = field.Metadata.Units ?? string.Empty;
        UnitText = field.Metadata.Units ?? string.Empty;
        OriginalValue = (float)field.OriginalValue;
        LiveValue = (float)field.LiveValue;
        Value = (float)field.PendingValue;
        Min = field.Metadata.Minimum is { } minimum ? (float)minimum : float.MinValue;
        Max = field.Metadata.Maximum is { } maximum ? (float)maximum : float.MaxValue;
        Increment = field.Metadata.Increment?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        stepSize = field.Metadata.Increment is > 0 ? (float)field.Metadata.Increment.Value : 0.1f;
        IsReadOnly = field.Metadata.ReadOnly;
        RebootRequired = field.Metadata.RebootRequired;
        ValidationError = field.ValidationError;
        WriteStatus = field.WriteStatus;
        WriteMessage = field.WriteMessage;
        IsModified = field.IsModified;

        ValuesItems = field.Metadata.Options
            .Select(option => new SelectItem(option.Label, (float)option.Value))
            .ToArray();
        ValuesData = ValuesItems.Select(option => option.Name).ToArray();
        HasValuesData = ValuesItems.Length > 0;
        SelectedValue = ValuesItems.FirstOrDefault(option => Math.Abs(option.Value - Value) < 0.0001f)?.Name;

        BitmaskOptions = field.Metadata.Bitmask
            .Where(option => option.Bit is >= 0 and < 64)
            .Select(option => new SelectItem(option.Label, 1UL << option.Bit))
            .ToArray();
        SelectedBitmaskItems.Clear();
        var selectedMask = (ulong)Math.Max(0, Math.Round(field.PendingValue));
        foreach (var option in field.Metadata.Bitmask.Where(option => option.Bit is >= 0 and < 64))
        {
            if ((selectedMask & (1UL << option.Bit)) != 0 && BitmaskOptions.FirstOrDefault(item => item.Name == option.Label) is { } selected)
            {
                SelectedBitmaskItems.Add(selected);
            }
        }

        HasBitmask = BitmaskOptions.Length > 0;
        HasNumericRangeData = !HasValuesData && !HasBitmask &&
                              (field.Metadata.Minimum is not null || field.Metadata.Maximum is not null);
        Range = field.Metadata.Minimum is null && field.Metadata.Maximum is null
            ? null
            : $"{field.Metadata.Minimum?.ToString(System.Globalization.CultureInfo.InvariantCulture)} {field.Metadata.Maximum?.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        RangeData = Range?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Options = HasValuesData
            ? string.Join(Environment.NewLine, field.Metadata.Options.Select(option => $"{option.Value}:{option.Label}"))
            : HasBitmask
                ? string.Join(Environment.NewLine, field.Metadata.Bitmask.Select(option => $"{option.Bit}:{option.Label}"))
                : null;
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
        LiveValue = parameter.Value;
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
            SelectedBitmaskItems.Clear();
            foreach (var item in BitmaskOptions.Where(item => ((int)Value & (int)item.Value) != 0))
            {
                SelectedBitmaskItems.Add(item);
            }
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
        if (editSession is not null)
        {
            editSession.TrySetPending(Name, value, out var error);
            ValidationError = error;
            if (editSession.GetField(Name) is { } current)
            {
                IsModified = current.IsModified;
                WriteStatus = current.WriteStatus;
                WriteMessage = current.WriteMessage;
            }
        }
    }

    /// <summary>
    /// Resets the value to the original vehicle value.
    /// </summary>
    public void ResetToOriginal()
    {
        if (editSession is null)
        {
            Value = OriginalValue;
            return;
        }

        editSession.Revert(Name);
        if (editSession.GetField(Name) is { } current)
        {
            SetField(current);
        }
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

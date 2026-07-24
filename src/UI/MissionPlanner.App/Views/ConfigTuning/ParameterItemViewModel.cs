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
    private ParameterFieldMetadata? editMetadata;
    private MavParamType? editType;

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
        if (loadingData || value is not IEnumerable<object> items)
        {
            return;
        }

        var selectedValue = 0.0f;
        foreach (var item in items.OfType<SelectItem>())
        {
            selectedValue += item.Value;
        }

        Value = selectedValue;
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
        ArgumentNullException.ThrowIfNull(metadata);
        loadingData = true;
        try
        {
            originalMetadata = metadata;
            editMetadata = null;
            editType = null;
            Value = 0f;
            ApplyEditorMetadata(CreateEditorMetadata(metadata));
            SelectedValue = ValuesItems?.FirstOrDefault()?.Name;
            SelectedBitmaskItems.Clear();
        }
        finally
        {
            loadingData = false;
        }
    }

    /// <summary>Updates this item from the shared editing-session field.</summary>
    /// <param name="field">The latest field projection.</param>
    public void SetField(ParameterEditField field)
    {
        var pendingValue = (float)field.PendingValue;
        var pendingValueChanged = Math.Abs(Value - pendingValue) > 0.0001f;
        var editorDefinitionChanged =
            editType != field.Type ||
            editMetadata != field.Metadata ||
            !string.Equals(Name, field.Name, StringComparison.Ordinal);

        loadingData = true;
        try
        {
            originalParameter = new VehicleParameter(field.Name, (float)field.OriginalValue, field.Type, 0, 0);
            originalMetadata = null;
            OriginalValue = (float)field.OriginalValue;
            LiveValue = (float)field.LiveValue;
            Value = pendingValue;
            ValidationError = field.ValidationError;
            WriteStatus = field.WriteStatus;
            WriteMessage = field.WriteMessage;
            IsModified = field.IsModified;

            if (editorDefinitionChanged)
            {
                ApplyEditorMetadata(CreateEditorMetadata(field));
                editMetadata = field.Metadata;
                editType = field.Type;
            }

            if (editorDefinitionChanged || pendingValueChanged)
            {
                SynchronizeSelections(field.PendingValue);
            }
        }
        finally
        {
            loadingData = false;
        }
    }

    private void ApplyEditorMetadata(EditorMetadataProjection metadata)
    {
        Name = metadata.Name;
        DisplayName = metadata.DisplayName;
        Description = metadata.Description ?? string.Empty;
        Units = metadata.Units ?? string.Empty;
        UnitText = metadata.UnitText ?? string.Empty;
        Range = metadata.Range;
        Values = metadata.Values;
        Bitmask = metadata.Bitmask;
        UserLevel = metadata.UserLevel;
        RebootRequired = metadata.RebootRequired;
        Min = metadata.Minimum ?? float.MinValue;
        Max = metadata.Maximum ?? float.MaxValue;
        Increment = metadata.Increment;
        stepSize = ResolveStepSize(metadata);

        ValuesItems = metadata.ValueOptions;
        ValuesData = ValuesItems.Select(option => option.Name).ToArray();
        HasValuesData = ValuesItems.Length > 0;

        BitmaskOptions = metadata.BitmaskOptions;
        HasBitmask = BitmaskOptions.Length > 0;
        RangeData = Range?.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        HasNumericRangeData = !HasValuesData && !HasBitmask && RangeData.Length == 2;
        Options = metadata.Options;

        // Bitmask values are edited through the multi-option control, not by entering a raw mask.
        IsReadOnly = metadata.ReadOnly || HasBitmask;
    }

    private void SynchronizeSelections(double pendingValue)
    {
        SelectedValue = ValuesItems?
            .FirstOrDefault(option => Math.Abs(option.Value - pendingValue) < 0.0001f)?
            .Name;

        SelectedBitmaskItems.Clear();
        var selectedMask = (ulong)Math.Max(0, Math.Round(pendingValue));
        foreach (var option in BitmaskOptions ?? [])
        {
            if ((selectedMask & (ulong)option.Value) != 0)
            {
                SelectedBitmaskItems.Add(option);
            }
        }
    }

    private static float ResolveStepSize(EditorMetadataProjection metadata)
    {
        if (metadata.IncrementValue is not null)
        {
            return metadata.IncrementValue.Value;
        }

        if (metadata.Increment is not null)
        {
            return 0.1f;
        }

        return metadata.Minimum is not null && metadata.Maximum is not null
            ? (float)Math.Round((metadata.Maximum.Value - metadata.Minimum.Value) / 10.0)
            : 0.1f;
    }

    private static EditorMetadataProjection CreateEditorMetadata(ParameterMetadata metadata)
    {
        var valueDefinitions = metadata.GetValueOptions()
            .OrderBy(option => option.Key)
            .ToArray();
        var valueOptions = valueDefinitions
            .Select(option => new SelectItem(option.Value, option.Key))
            .ToArray();
        var bitmaskDefinitions = metadata.GetBitmaskOptions()
            .Where(option => option.Key is >= 0 and < 64)
            .OrderBy(option => option.Key)
            .ToArray();
        var bitmaskOptions = bitmaskDefinitions
            .Select(option => new SelectItem(option.Value, 1UL << option.Key))
            .ToArray();
        var options = valueOptions.Length > 0
            ? string.Join(Environment.NewLine, valueDefinitions.Select(option => $"{option.Key}:{option.Value}"))
            : bitmaskOptions.Length > 0
                ? string.Join(Environment.NewLine, bitmaskDefinitions.Select(option => $"{option.Key}:{option.Value}"))
                : null;

        return new EditorMetadataProjection(
            metadata.Name,
            metadata.DisplayName,
            metadata.Description,
            metadata.Units,
            metadata.UnitText,
            metadata.Range,
            metadata.Values,
            metadata.Bitmask,
            metadata.Increment,
            metadata.UserLevel,
            metadata.MinValue,
            metadata.MaxValue,
            metadata.IncrementValue,
            metadata.ReadOnly,
            metadata.RebootRequired,
            valueOptions,
            bitmaskOptions,
            options);
    }

    private static EditorMetadataProjection CreateEditorMetadata(ParameterEditField field)
    {
        var metadata = field.Metadata;
        var valueOptions = metadata.Options
            .Select(option => new SelectItem(option.Label, (float)option.Value))
            .ToArray();
        var bitmaskDefinitions = metadata.Bitmask
            .Where(option => option.Bit is >= 0 and < 64)
            .ToArray();
        var bitmaskOptions = bitmaskDefinitions
            .Select(option => new SelectItem(option.Label, 1UL << option.Bit))
            .ToArray();
        var range = metadata.RangeText ?? CreateRangeText(metadata.Minimum, metadata.Maximum);
        var values = metadata.ValuesText ?? (valueOptions.Length > 0
            ? string.Join(",", metadata.Options.Select(option => $"{option.Value}:{option.Label}"))
            : null);
        var bitmask = metadata.BitmaskText ?? (bitmaskOptions.Length > 0
            ? string.Join(",", bitmaskDefinitions.Select(option => $"{option.Bit}:{option.Label}"))
            : null);
        var increment = metadata.IncrementText ??
                        metadata.Increment?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var options = valueOptions.Length > 0
            ? string.Join(Environment.NewLine, metadata.Options.Select(option => $"{option.Value}:{option.Label}"))
            : bitmaskOptions.Length > 0
                ? string.Join(Environment.NewLine, bitmaskDefinitions.Select(option => $"{option.Bit}:{option.Label}"))
                : null;

        return new EditorMetadataProjection(
            field.Name,
            metadata.DisplayName ?? field.Name,
            metadata.Description,
            metadata.Units,
            metadata.UnitText,
            range,
            values,
            bitmask,
            increment,
            metadata.UserLevel,
            metadata.Minimum is { } minimum ? (float)minimum : null,
            metadata.Maximum is { } maximum ? (float)maximum : null,
            metadata.Increment is { } incrementValue ? (float)incrementValue : null,
            metadata.ReadOnly,
            metadata.RebootRequired,
            valueOptions,
            bitmaskOptions,
            options);
    }

    private static string? CreateRangeText(double? minimum, double? maximum)
    {
        if (minimum is null && maximum is null)
        {
            return null;
        }

        return $"{minimum?.ToString(System.Globalization.CultureInfo.InvariantCulture)} {maximum?.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Sets the data for this parameter from a VehicleParameter instance.
    /// </summary>
    /// <param name="parameter">The VehicleParameter instance containing the data.</param>
    public void SetData(VehicleParameter parameter)
    {
        editMetadata = null;
        editType = null;
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

    private sealed record EditorMetadataProjection(
        string Name,
        string? DisplayName,
        string? Description,
        string? Units,
        string? UnitText,
        string? Range,
        string? Values,
        string? Bitmask,
        string? Increment,
        string? UserLevel,
        float? Minimum,
        float? Maximum,
        float? IncrementValue,
        bool ReadOnly,
        bool RebootRequired,
        SelectItem[] ValueOptions,
        SelectItem[] BitmaskOptions,
        string? Options);
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

using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Library.Math;
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
    private ParameterEditField? projectedField;

    private bool loadingData;

    private double stepSize = 0.1;

    /// <summary>
    /// Provides the public API for OriginalParameter.
    /// </summary>
    public VehicleParameter? OriginalParameter => originalParameter;

    /// <summary>
    /// Command that is triggered when the selected values change.
    /// </summary>
    public ICommand SelectedValuesChanged
    {
        get;
    }

    [DisplayName("Default")]
    [ObservableProperty]
    public partial double OriginalValue
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string Name
    {
        get;
        set;
    } = null!;

    [ObservableProperty]
    public partial string? DisplayName
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double Value
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double LiveValue
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double StepSize
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? SelectedValue
    {
        get;
        set;
    }

    /// <summary>
    /// Gets a value indicating whether the parameter is a signed byte.
    /// </summary>
    public bool IsByte => OriginalParameter!.Type == MavParamType.Int8;

    /// <summary>
    /// Gets a value indicating whether the parameter is an unsigned byte.
    /// </summary>
    public bool IsUnsignedByte => OriginalParameter.Type == MavParamType.Uint8;

    /// <summary>
    /// Gets a value indicating whether the parameter is a signed integer.
    /// </summary>
    public bool IsInteger => OriginalParameter!.Type == MavParamType.Int16 || OriginalParameter.Type == MavParamType.Int32;

    /// <summary>
    /// Gets a value indicating whether the parameter is an unsigned integer.
    /// </summary>
    public bool IsUnsignedInteger => OriginalParameter.Type is MavParamType.Uint16 or MavParamType.Uint32;

    /// <summary>
    /// Gets a value indicating whether the parameter is a floating-point number.
    /// </summary>
    public bool IsFloat => OriginalParameter!.Type == MavParamType.Real32;


    /// <summary>
    /// Gets a value indicating whether the parameter is a numeric range data.
    /// </summary>
    public bool IsFloatRangeData => IsFloat && HasNumericRangeData;

    /// <summary>
    /// Gets a value indicating whether the parameter is an unsigned integer range data.
    /// </summary>
    public bool IsUnsignedIntegerRangeData => IsUnsignedInteger && HasNumericRangeData;

    /// <summary>
    /// Gets a value indicating whether the parameter is an integer range data.
    /// </summary>
    public bool IsIntegerRangeData => IsInteger && HasNumericRangeData;

    /// <summary>
    /// Gets a value indicating whether the parameter is an unsigned byte range data.
    /// </summary>
    public bool IsUnsignedByteRangeData => IsUnsignedByte && HasNumericRangeData;

    /// <summary>
    /// Gets a value indicating whether the parameter is a byte range data.
    /// </summary>
    public bool IsByteRangeData => IsByte && HasNumericRangeData;


    /// <summary>
    /// Gets or sets the culture-aware text displayed by the unrestricted value editor.
    /// </summary>
    /// <remarks>
    /// Parameter values are transported as MAVLink single-precision values. This property
    /// keeps the UI model as <see cref="double"/> while hiding the binary expansion caused
    /// when a single-precision value is projected into that type.
    /// </remarks>
    [DataGridIgnore]
    public string ValueText
    {
        get => FormatParameterValue(Value);
        set
        {
            if (TryParseParameterValue(value, out var parsed) &&
                !RepresentSameParameterValue(Value, parsed))
            {
                Value = parsed;
            }
        }
    }

    /// <summary>
    /// Gets the culture-aware text displayed for the original parameter value.
    /// </summary>
    [DataGridIgnore]
    public string OriginalValueText => FormatParameterValue(OriginalValue);

    [ObservableProperty]
    public partial double Max
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double Min
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? Units
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? UnitText
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? Values
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? Range
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? Description
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? Options
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string[]? ValuesData
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string[]? RangeData
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial SelectItem[]? ValuesItems
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial SelectItem[]? BitmaskOptions
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial ObservableRangeCollection<object> SelectedBitmaskItems
    {
        get;
        set;
    } = new();

    [DataGridIgnore]
    [ObservableProperty]
    public partial string? Increment
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial string? UserLevel
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial string? Bitmask
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial bool IsModified
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial bool IsReadOnly
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial bool RebootRequired
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial bool HasValuesData
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial bool HasNumericRangeData
    {
        get;
        set;
    }


    [DataGridIgnore]
    [ObservableProperty]
    public partial bool HasBitmask
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial string? ValidationError
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial ParameterEditWriteStatus WriteStatus
    {
        get;
        set;
    }

    [DataGridIgnore]
    [ObservableProperty]
    public partial string? WriteMessage
    {
        get;
        set;
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

    partial void OnSelectedValueChanged(string? value)
    {
        if (float.TryParse(value, out var result))
        {
            Value = result;
        }
    }

    private void OnSelectedValuesChanged(object value)
    {
        if (loadingData || value is not IEnumerable<object> items)
        {
            return;
        }

        var selectedValue = 0.0;
        foreach (var item in items.OfType<SelectItem>())
        {
            selectedValue += item.Value;
        }

        Value = selectedValue;
    }

    /// <summary>Updates this item from the shared editing-session field.</summary>
    /// <param name="field">The latest field projection.</param>
    public void SetField(ParameterEditField field)
    {
        if (field == projectedField)
        {
            return;
        }

        var pendingValue = field.PendingValue;
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
            OriginalValue = field.OriginalValue;
            LiveValue = field.LiveValue;
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

            PreserveCurrentValueInEditorBounds(pendingValue);

            if (editorDefinitionChanged || pendingValueChanged)
            {
                SynchronizeSelections(field.PendingValue);
            }
        }
        finally
        {
            loadingData = false;
            projectedField = field;
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
        Increment = metadata.Increment;
        ValuesItems = metadata.ValueOptions;
        ValuesData = ValuesItems.Select(option => option.Name).ToArray();
        HasValuesData = ValuesItems.Length > 0;

        BitmaskOptions = metadata.BitmaskOptions;
        HasBitmask = BitmaskOptions.Length > 0;
        RangeData = Range?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var minimum = ResolveMetadataNumber(RangeData, 0, metadata.Minimum);
        var maximum = ResolveMetadataNumber(RangeData, 1, metadata.Maximum);
        Min = minimum ?? double.MinValue;
        Max = maximum ?? double.MaxValue;
        stepSize = ResolveStepSize(metadata, minimum, maximum);
        StepSize = stepSize;
        HasNumericRangeData = !HasValuesData && !HasBitmask && RangeData.Length == 2;
        Options = metadata.Options;

        // Bitmask values are edited through the multi-option control, not by entering a raw mask.
        IsReadOnly = metadata.ReadOnly || HasBitmask;
    }

    private void PreserveCurrentValueInEditorBounds(double value)
    {
        if (!HasNumericRangeData || !double.IsFinite(value))
        {
            return;
        }

        // Some ArduPilot parameters use an out-of-range value such as zero as a
        // firmware-defined sentinel. Keep it representable so control coercion
        // cannot turn row realization into a user edit.
        Min = Math.Min(Min, value);
        Max = Math.Max(Max, value);
    }

    private void SynchronizeSelections(double pendingValue)
    {
        SelectedValue = ValuesItems?
            .FirstOrDefault(option => Math.Abs(option.Value - pendingValue) < 0.0001f)?
            .Name;

        var selectedBitmaskItems = new List<object>();
        var selectedMask = (ulong)Math.Max(0, Math.Round(pendingValue));
        foreach (var option in BitmaskOptions ?? [])
        {
            if ((selectedMask & (ulong)option.Value) != 0)
            {
                selectedBitmaskItems.Add(option);
            }
        }

        SelectedBitmaskItems.ReplaceRange(selectedBitmaskItems);
    }

    private static double ResolveStepSize(EditorMetadataProjection metadata, double? minimum, double? maximum)
    {
        var resolved = TryParseInvariant(metadata.Increment, out var parsedIncrement)
            ? parsedIncrement
            : metadata.IncrementValue is not null
                ? metadata.IncrementValue.Value
                : metadata.Increment is not null
                    ? 0.1
                    : minimum is not null && maximum is not null
                        ? Math.Round((maximum.Value - minimum.Value) / 10.0)
                        : 0.1;

        return double.IsFinite(resolved) && resolved > 0 ? resolved : 0.1;
    }

    private static double? ResolveMetadataNumber(IReadOnlyList<string> values, int index, double? fallback)
    {
        return index < values.Count &&
               TryParseInvariant(values[index], out var parsed)
            ? parsed
            : fallback;
    }

    private static bool TryParseInvariant(string? text, out double value)
    {
        return double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static EditorMetadataProjection CreateEditorMetadata(ParameterEditField field)
    {
        var metadata = field.Metadata;
        var valueOptions = metadata.Options
            .Select(option => new SelectItem(option.Label, option.Value))
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
                        metadata.Increment?.ToString(CultureInfo.InvariantCulture);
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
            metadata.Minimum,
            metadata.Maximum,
            metadata.Increment,
            metadata.ReadOnly,
            metadata.RebootRequired,
            valueOptions,
            bitmaskOptions,
            options);
    }

    private static string? CreateRangeText(double? minimum, double? maximum)
    {
        return minimum is null && maximum is null
            ? null
            : $"{minimum?.ToString(CultureInfo.InvariantCulture)}-{maximum?.ToString(CultureInfo.InvariantCulture)}";
    }


    [RelayCommand]
    private void IncrementNumber()
    {
        StepNumber(1);
    }

    [RelayCommand]
    private void DecrementNumber()
    {
        StepNumber(-1);
    }

    private void StepNumber(int direction)
    {
        if (direction is not (-1 or 1) ||
            !double.IsFinite(Value) ||
            !double.IsFinite(stepSize) ||
            stepSize <= 0d ||
            !double.IsFinite(Min) ||
            !double.IsFinite(Max) ||
            Min > Max)
        {
            return;
        }

        double steppedValue;

        try
        {
            var decimalValue = Convert.ToDecimal(Value);
            var decimalStep = Convert.ToDecimal(stepSize);

            steppedValue = Convert.ToDouble(
                decimalValue + (direction * decimalStep));
        }
        catch (OverflowException)
        {
            steppedValue = Value + (direction * stepSize);
        }

        Value = Math.Clamp(steppedValue, Min, Max);
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
            if (item is not null &&

                // Math.Abs(item.Value - Value) > 0.0001f)
                MathUtils.AreNearlyEqual(item.Value, Value) == false)
            {
                Value = item.Value;
            }
        }
    }


    /// <summary>
    /// Updates the formatted original-value projection.
    /// </summary>
    partial void OnOriginalValueChanged(double value)
    {
        OnPropertyChanged(nameof(OriginalValueText));
    }

    /// <summary>
    /// Checks if the value has been modified from the original.
    /// </summary>
    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(ValueText));

        if (loadingData)
        {
            return;
        }

        IsModified = MathUtils.AreNearlyEqual(value, OriginalValue) == false;
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

    private static string FormatParameterValue(double value)
    {
        var culture = CultureInfo.CurrentCulture;

        if (!double.IsFinite(value) ||
            value > float.MaxValue ||
            value < -float.MaxValue)
        {
            return value.ToString("G15", culture);
        }

        // MAVLink PARAM_VALUE is a float32. Formatting its single-precision projection
        // restores the concise representation used before the UI properties became double,
        // while the bound numeric value itself remains a double.
        return ((float)value).ToString(culture);
    }

    private static bool TryParseParameterValue(string? text, out double value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        const NumberStyles styles = NumberStyles.Float;
        var culture = CultureInfo.CurrentCulture;

        return double.TryParse(text, styles, culture, out value) ||
               (!Equals(culture, CultureInfo.InvariantCulture) &&
                double.TryParse(text, styles, CultureInfo.InvariantCulture, out value));
    }

    private static bool RepresentSameParameterValue(double left, double right)
    {
        return double.IsFinite(left) &&
               double.IsFinite(right) &&
               left is >= -float.MaxValue and <= float.MaxValue &&
               right is >= -float.MaxValue and <= float.MaxValue
            ? ((float)left).Equals((float)right)
            : left.Equals(right);
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
        double? Minimum,
        double? Maximum,
        double? IncrementValue,
        bool ReadOnly,
        bool RebootRequired,
        SelectItem[] ValueOptions,
        SelectItem[] BitmaskOptions,
        string? Options);
}

/// <summary>
/// Provides the public API for SelectItem.
/// </summary>
public sealed record SelectItem(string Name, double Value)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}

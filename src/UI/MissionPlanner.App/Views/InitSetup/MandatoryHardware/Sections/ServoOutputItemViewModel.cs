using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents one physical servo output with editable parameters and live PWM.</summary>
public sealed partial class ServoOutputItemViewModel : ObservableObject
{
    private readonly Action<ServoOutputItemViewModel> dirtyChanged;
    private bool suppressDirtyTracking;
    private bool originalReversed;
    private int originalFunction;
    private int originalMinimum;
    private int originalTrim;
    private int originalMaximum;
    private IReadOnlySet<string>? availableFields;

    /// <summary>Gets whether function assignment is supported.</summary>
    public bool HasFunction => availableFields is null || availableFields.Contains("FUNCTION");
    /// <summary>Gets whether reversal is supported.</summary>
    public bool HasReverse => availableFields is null || availableFields.Contains("REVERSED");
    /// <summary>Gets whether minimum PWM is supported.</summary>
    public bool HasMinimum => availableFields is null || availableFields.Contains("MIN");
    /// <summary>Gets whether trim PWM is supported.</summary>
    public bool HasTrim => availableFields is null || availableFields.Contains("TRIM");
    /// <summary>Gets whether maximum PWM is supported.</summary>
    public bool HasMaximum => availableFields is null || availableFields.Contains("MAX");
    /// <summary>Gets the live PWM used by the bar.</summary>
    [ObservableProperty]
    public partial int LivePwm
    {
        get; private set;
    }
    /// <summary>Gets whether live output telemetry is available.</summary>
    [ObservableProperty]
    public partial bool HasLivePwm
    {
        get; private set;
    }

    /// <summary>Initializes a servo output row.</summary>
    /// <param name="info">The output projection.</param>
    /// <param name="options">The available functions.</param>
    /// <param name="dirtyChanged">Called when the row's dirty state changes.</param>
    public ServoOutputItemViewModel(
        ServoOutputInfo info,
        IReadOnlyList<ServoFunctionOption> options,
        Action<ServoOutputItemViewModel> dirtyChanged)
    {
        this.dirtyChanged = dirtyChanged;
        ChannelNumber = info.ChannelNumber;
        options = info.FunctionOptions ?? options;
        var f = options.Any(option => option.Value == info.FunctionValue)
            ? options
            : options.Append(new ServoFunctionOption(info.FunctionValue, info.FunctionName)).ToArray();

        Functions = f.OrderBy(option => option.Name).ToArray();

        ApplyConfiguration(info);
    }

    /// <summary>Gets whether an editable value differs from its last confirmed value.</summary>
    [ObservableProperty]
    public partial bool IsDirty
    {
        get; private set;
    }

    /// <summary>Gets the one-based physical output channel.</summary>
    public int ChannelNumber
    {
        get;
    }

    /// <summary>Gets or sets whether the output is reversed.</summary>
    [ObservableProperty]
    public partial bool Reversed
    {
        get; set;
    }

    /// <summary>Gets or sets the minimum PWM.</summary>
    [ObservableProperty]
    public partial int MinimumPwm
    {
        get; set;
    }

    /// <summary>Gets or sets the trim PWM.</summary>
    [ObservableProperty]
    public partial int TrimPwm
    {
        get; set;
    }

    /// <summary>Gets or sets the maximum PWM.</summary>
    [ObservableProperty]
    public partial int MaximumPwm
    {
        get; set;
    }

    /// <summary>Gets the lowest allowed PWM value.</summary>
    [ObservableProperty]
    public partial int AllowedMinimumPwm
    {
        get; private set;
    }

    /// <summary>Gets the highest allowed PWM value.</summary>
    [ObservableProperty]
    public partial int AllowedMaximumPwm
    {
        get; private set;
    }

    /// <summary>Gets the available function options.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ServoFunctionOption> Functions { get; private set; } = [];

    /// <summary>Gets the live PWM description.</summary>
    [ObservableProperty]
    public partial string LiveDescription { get; private set; } = string.Empty;

    /// <summary>Gets or sets the selected function.</summary>
    [ObservableProperty]
    public partial ServoFunctionOption? SelectedFunction
    {
        get; set;
    }

    /// <summary>Gets the output header.</summary>
    public string Header => $"# {ChannelNumber}";

    /// <summary>Gets the desired settings represented by the row.</summary>
    public ServoOutputSettings Settings => new(
        ChannelNumber,
        Reversed,
        SelectedFunction?.Value ?? originalFunction,
        MinimumPwm,
        TrimPwm,
        MaximumPwm)
    {
        AvailableFields = availableFields
    };

    /// <summary>Updates live PWM without affecting editable state or dirty tracking.</summary>
    /// <param name="info">The latest output projection.</param>
    public void UpdateLive(ServoOutputInfo info)
    {
        UpdateLive(info.LivePwm, info.IsStale);
    }

    /// <summary>Updates live PWM without rebuilding the output configuration.</summary>
    public void UpdateLive(int? livePwm, bool isStale)
    {
        HasLivePwm = livePwm.HasValue && !isStale;
        LivePwm = livePwm ?? 0;
        LiveDescription = livePwm is { } pwm
            ? $"{pwm} µs{(isStale ? " (stale)" : string.Empty)}"
            : "—";
    }

    /// <summary>Refreshes confirmed configuration when the row has no unsaved edits.</summary>
    /// <param name="info">The latest output projection.</param>
    public void Refresh(ServoOutputInfo info)
    {
        UpdateLive(info);
        if (!IsDirty)
        {
            ApplyConfiguration(info);
        }
    }

    /// <summary>Marks current editable values as successfully confirmed.</summary>
    public void AcceptChanges()
    {
        originalReversed = Reversed;
        originalFunction = SelectedFunction?.Value ?? originalFunction;
        originalMinimum = MinimumPwm;
        originalTrim = TrimPwm;
        originalMaximum = MaximumPwm;
        UpdateDirtyState();
    }

    partial void OnReversedChanged(bool value) => UpdateDirtyState();

    partial void OnMinimumPwmChanged(int value) => UpdateDirtyState();

    partial void OnTrimPwmChanged(int value) => UpdateDirtyState();

    partial void OnMaximumPwmChanged(int value) => UpdateDirtyState();

    partial void OnSelectedFunctionChanged(ServoFunctionOption? value) => UpdateDirtyState();

    partial void OnIsDirtyChanged(bool value)
    {
        if (!suppressDirtyTracking)
        {
            dirtyChanged(this);
        }
    }

    private void ApplyConfiguration(ServoOutputInfo info)
    {
        suppressDirtyTracking = true;
        var options = info.FunctionOptions ?? Functions;
        Functions = options.Any(option => option.Value == info.FunctionValue)
            ? options : options.Append(new ServoFunctionOption(info.FunctionValue, info.FunctionName)).ToArray();
        availableFields = info.AvailableFields;
        OnPropertyChanged(nameof(HasFunction));
        OnPropertyChanged(nameof(HasReverse));
        OnPropertyChanged(nameof(HasMinimum));
        OnPropertyChanged(nameof(HasTrim));
        OnPropertyChanged(nameof(HasMaximum));
        UpdateLive(info);
        Reversed = info.Reversed;
        MinimumPwm = info.MinimumPwm;
        TrimPwm = info.TrimPwm;
        MaximumPwm = info.MaximumPwm;
        AllowedMinimumPwm = info.AllowedMinimumPwm;
        AllowedMaximumPwm = info.AllowedMaximumPwm;
        SelectedFunction = Functions.First(option => option.Value == info.FunctionValue);
        originalReversed = Reversed;
        originalFunction = info.FunctionValue;
        originalMinimum = MinimumPwm;
        originalTrim = TrimPwm;
        originalMaximum = MaximumPwm;
        IsDirty = false;
        suppressDirtyTracking = false;
    }

    private void UpdateDirtyState()
    {
        if (suppressDirtyTracking)
        {
            return;
        }

        IsDirty = (HasReverse && Reversed != originalReversed) ||
            (HasFunction && (SelectedFunction?.Value ?? originalFunction) != originalFunction) ||
            (HasMinimum && MinimumPwm != originalMinimum) ||
            (HasTrim && TrimPwm != originalTrim) ||
            (HasMaximum && MaximumPwm != originalMaximum);
    }
}


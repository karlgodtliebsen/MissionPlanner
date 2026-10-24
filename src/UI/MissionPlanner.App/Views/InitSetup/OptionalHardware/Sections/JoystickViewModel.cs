using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>One local axis mapping and live calibration evidence.</summary>
public sealed partial class JoystickAxisEditor(JoystickAxisMapping mapping) : ObservableObject
{
    /// <summary>Gets the control function.</summary>
    public JoystickFunction Function { get; } = mapping.Function;
    /// <summary>Gets or sets the zero-based physical axis.</summary>
    [ObservableProperty] public partial int AxisIndex { get; set; } = mapping.AxisIndex;
    /// <summary>Gets or sets the measured minimum.</summary>
    [ObservableProperty] public partial double Minimum { get; set; } = mapping.Minimum;
    /// <summary>Gets or sets the measured center.</summary>
    [ObservableProperty] public partial double Center { get; set; } = mapping.Center;
    /// <summary>Gets or sets the measured maximum.</summary>
    [ObservableProperty] public partial double Maximum { get; set; } = mapping.Maximum;
    /// <summary>Gets or sets the normalized dead zone.</summary>
    [ObservableProperty] public partial double DeadZone { get; set; } = mapping.DeadZone;
    /// <summary>Gets or sets axis reversal.</summary>
    [ObservableProperty] public partial bool Reverse { get; set; } = mapping.Reverse;
    /// <summary>Gets or sets cubic response blending.</summary>
    [ObservableProperty] public partial double Expo { get; set; } = mapping.Expo;
    /// <summary>Gets the most recent physical reading.</summary>
    [ObservableProperty] public partial double Raw { get; private set; }
    /// <summary>Gets the most recent normalized reading.</summary>
    [ObservableProperty] public partial double Normalized { get; private set; }
    /// <summary>Builds an immutable mapping.</summary>
    public JoystickAxisMapping Build() => new(Function, AxisIndex, Minimum, Center, Maximum, DeadZone, Reverse, Expo);
    /// <summary>Updates live evidence and optional range capture.</summary>
    public void Observe(double raw, bool capture)
    {
        Raw = raw;
        if (capture) { Minimum = Math.Min(Minimum, raw); Maximum = Math.Max(Maximum, raw); }
        try { Normalized = JoystickCalibration.Normalize(raw, Build()); }
        catch (ArgumentException) { Normalized = 0; }
    }
}

/// <summary>Previews and calibrates local joystick input, with explicitly confirmed vehicle output.</summary>
public sealed partial class JoystickViewModel : OptionalHardwareBaseViewModel
{
    private readonly IJoystickProvider provider;
    private readonly JoystickControlSession controller;
    private readonly IPlannerSettingsService settings;
    private readonly IUserConfirmationService confirmation;
    private CancellationTokenSource lifetime = new();
    private JoystickState? latest;
    private int uiPending;
    private bool subscribed;

    /// <summary>Creates a joystick page with the platform and vehicle boundaries.</summary>
    public JoystickViewModel(IJoystickProvider provider, JoystickControlSession controller,
        IPlannerSettingsService settings, IUserConfirmationService confirmation, ILogger<JoystickViewModel> logger) : base(logger)
    {
        this.provider = provider;
        this.controller = controller;
        this.settings = settings;
        this.confirmation = confirmation;
        var profile = settings.Current.Joystick ?? new();
        foreach (var mapping in profile.Axes) Axes.Add(new(mapping));
        DeadmanButton = profile.DeadmanButton;
        ButtonMappings = string.Join(",", profile.Buttons);
        SelectedOutputMode = profile.OutputMode;
        SavedDeviceName = profile.DeviceName;
    }

    /// <summary>Gets available local devices.</summary>
    public ObservableCollection<JoystickDeviceDescriptor> Devices { get; } = [];
    /// <summary>Gets four configurable axes.</summary>
    public ObservableCollection<JoystickAxisEditor> Axes { get; } = [];
    /// <summary>Gets supported output choices.</summary>
    public IReadOnlyList<JoystickOutputMode> OutputModes { get; } = Enum.GetValues<JoystickOutputMode>();
    /// <summary>Gets or sets the local device.</summary>
    [ObservableProperty] public partial JoystickDeviceDescriptor? SelectedDevice { get; set; }
    /// <summary>Gets whether vehicle output is currently enabled.</summary>
    [ObservableProperty] public partial bool VehicleOutputEnabled { get; private set; }
    /// <summary>Gets whether local preview owns the current mapping.</summary>
    [ObservableProperty] public partial bool IsPreviewRunning { get; private set; }
    /// <summary>Gets or sets the output protocol for the next preview session.</summary>
    [ObservableProperty] public partial JoystickOutputMode SelectedOutputMode { get; set; }
    /// <summary>Gets or sets the required physical deadman button.</summary>
    [ObservableProperty] public partial int DeadmanButton { get; set; }
    /// <summary>Gets or sets physical button indices assigned to output bits; -1 leaves a bit unused.</summary>
    [ObservableProperty] public partial string ButtonMappings { get; set; } = string.Empty;
    /// <summary>Gets or sets explicit calibration review for this page session.</summary>
    [ObservableProperty] public partial bool CalibrationReviewed { get; set; }
    /// <summary>Gets whether range capture is active.</summary>
    [ObservableProperty] public partial bool IsCalibrating { get; private set; }
    /// <summary>Gets a live button list.</summary>
    [ObservableProperty] public partial string PressedButtons { get; private set; } = "None";
    /// <summary>Gets the name of the device associated with saved calibration.</summary>
    [ObservableProperty] public partial string SavedDeviceName { get; private set; } = string.Empty;
    /// <summary>Gets platform input support.</summary>
    public string PlatformStatus => provider.IsSupported ? "SDL desktop joystick input. Preview starts locally; vehicle output requires a separate confirmation." : "Joystick input is unavailable on this platform.";

    private JoystickProfile Profile() => new()
    {
        Axes = Axes.Select(axis => axis.Build()).ToArray(), DeadmanButton = DeadmanButton,
        Buttons = ButtonMappings.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray(),
        OutputMode = SelectedOutputMode, CalibrationReviewed = CalibrationReviewed,
        DeviceName = SelectedDevice?.Name ?? string.Empty,
    };

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var devices = await provider.EnumerateAsync(linked.Token);
            Devices.Clear();
            foreach (var device in devices) Devices.Add(device);
            SelectedDevice = Devices.FirstOrDefault();
            SetMessages($"{Devices.Count} local device(s) found. Review the selected device and mapping.", null);
        }
        catch (OperationCanceledException) { SetMessages("Enumeration cancelled.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task PreviewAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await controller.StopAsync();
        IsPreviewRunning = false;
            var device = SelectedDevice ?? throw new InvalidOperationException("Select a local joystick.");
            cancellationToken.ThrowIfCancellationRequested();
            await controller.StartAsync(device.Id, Profile(), lifetime.Token);
            IsPreviewRunning = true;
            SetMessages("Local preview active. Stop and restart preview after changing mappings. Positive pitch is nose-up; throttle -1 is minimum.", null);
        }
        catch (OperationCanceledException) { SetMessages("Preview cancelled.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CaptureRangeAsync()
    {

        try
        {
            await controller.DisableAsync(CancellationToken.None);
            if (!controller.IsRunning) { SetMessages("Start local preview before measuring axis ranges.", null); return; }
            CalibrationReviewed = false;
            foreach (var axis in Axes) { axis.Minimum = axis.Maximum = axis.Raw; }
            IsCalibrating = true;
            SetMessages("Move every axis through its full range. Return roll/pitch/yaw to center, then finish calibration.", null);
        }
        catch (Exception exception) { SetMessages(exception); }
        finally { VehicleOutputEnabled = controller.OutputEnabled; IsPreviewRunning = controller.IsRunning; }
    }

    [RelayCommand]
    private async Task FinishCalibrationAsync()
    {

        try
        {
            IsCalibrating = false;
            await controller.StopAsync();
            IsPreviewRunning = false;
            foreach (var axis in Axes) axis.Center = axis.Function == JoystickFunction.Throttle ? (axis.Minimum + axis.Maximum) / 2 : axis.Raw;
            SetMessages("Range capture finished. Check limits, center, reversal and expo. Review calibration, then restart preview to use these values.", null);
        }
        catch (Exception exception) { SetMessages(exception); }
        finally { VehicleOutputEnabled = controller.OutputEnabled; IsPreviewRunning = controller.IsRunning; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = Profile();
            profile.Validate(SelectedDevice ?? throw new InvalidOperationException("Select a device first."));
            var result = await settings.SaveAsync(settings.Current with { Joystick = profile }, cancellationToken);
            if (!result.Success) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Message)));
            SavedDeviceName = profile.DeviceName;
            SetMessages("Local joystick mapping saved. Vehicle output remains an explicit action each time.", null);
        }
        catch (OperationCanceledException) { SetMessages("Saving cancelled.", null); }
        catch (Exception ex) { SetMessages(ex); }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task EnableAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            if (!await confirmation.ConfirmAsync("Enable joystick vehicle control",
                "Keep the vehicle disarmed for initial verification. Review calibration, lower throttle below 5% and hold the configured deadman button. Verify the firmware GCS system ID, RC override timeout and failsafe behavior. Releasing the deadman disables output and releases GCS RC overrides.", "Enable control", linked.Token)) return;
            await controller.EnableAsync(true, linked.Token);
            VehicleOutputEnabled = controller.OutputEnabled;
        }
        catch (OperationCanceledException) { SetMessages("Enable cancelled.", null); }
        catch (Exception ex) { SetMessages(ex); }
    }

    [RelayCommand]
    private async Task DisableAsync()
    {
        try { await controller.DisableAsync(CancellationToken.None); SetMessages("Vehicle output disabled and release requested.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { VehicleOutputEnabled = false; }
    }

    [RelayCommand]
    private async Task StopPreviewAsync()
    {

        try
        {
            await controller.StopAsync();
            IsPreviewRunning = false;
            IsCalibrating = VehicleOutputEnabled = false;
        }
        catch (Exception exception) { SetMessages(exception); }
        finally { VehicleOutputEnabled = controller.OutputEnabled; IsPreviewRunning = controller.IsRunning; }
    }

    private void OnSample(JoystickState state, JoystickVehicleCommand command)
    {
        Volatile.Write(ref latest, state);
        if (Interlocked.Exchange(ref uiPending, 1) != 0) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref uiPending, 0);
            var sample = Volatile.Read(ref latest);
            if (lifetime.IsCancellationRequested || sample is null) return;
            foreach (var axis in Axes)
                if (sample.Axes.FirstOrDefault(value => value.Index == axis.AxisIndex) is { } value) axis.Observe(value.RawValue, IsCalibrating);
            PressedButtons = string.Join(", ", sample.Buttons.Where(button => button.IsPressed).Select(button => button.Index));
            VehicleOutputEnabled = controller.OutputEnabled;
        });
    }

    private void OnStatus(string message) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        if (!lifetime.IsCancellationRequested)
        {
            VehicleOutputEnabled = controller.OutputEnabled;
            if (message.StartsWith("Joystick stopped", StringComparison.Ordinal) || message.Contains("timed out", StringComparison.Ordinal)) IsPreviewRunning = false;
            SetMessages(message, null);
        }
    });

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        if (!subscribed) { controller.Sampled += OnSample; controller.StatusChanged += OnStatus; subscribed = true; }
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel();
        await controller.StopAsync();
        IsPreviewRunning = false;
        VehicleOutputEnabled = IsCalibrating = false;
        if (subscribed) { controller.Sampled -= OnSample; controller.StatusChanged -= OnStatus; subscribed = false; }
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lifetime.Cancel();
        controller.Sampled -= OnSample;
        controller.StatusChanged -= OnStatus;
        base.Dispose();
    }
}

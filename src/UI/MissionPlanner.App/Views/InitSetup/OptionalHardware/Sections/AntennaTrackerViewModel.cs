using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Editable local calibration for one standalone tracker axis.</summary>
public sealed partial class TrackerAxisEditor(TrackerAxisCalibration calibration) : ObservableObject
{
    /// <summary>Gets or sets the minimum mechanical angle.</summary>
    [ObservableProperty] public partial double MinimumDegrees { get; set; } = calibration.MinimumDegrees;
    /// <summary>Gets or sets the maximum mechanical angle.</summary>
    [ObservableProperty] public partial double MaximumDegrees { get; set; } = calibration.MaximumDegrees;
    /// <summary>Gets or sets the lower PWM endpoint in microseconds.</summary>
    [ObservableProperty] public partial int MinimumPwm { get; set; } = calibration.MinimumPwm;
    /// <summary>Gets or sets the upper PWM endpoint in microseconds.</summary>
    [ObservableProperty] public partial int MaximumPwm { get; set; } = calibration.MaximumPwm;
    /// <summary>Gets or sets direction reversal.</summary>
    [ObservableProperty] public partial bool Reversed { get; set; } = calibration.Reversed;
    /// <summary>Captures immutable calibration for a run.</summary>
    public TrackerAxisCalibration Build() => new() { MinimumDegrees = MinimumDegrees, MaximumDegrees = MaximumDegrees, MinimumPwm = MinimumPwm, MaximumPwm = MaximumPwm, Reversed = Reversed };
}

/// <summary>Separates ArduPilot tracker configuration/tests from a serial tracker following an aircraft.</summary>
public sealed partial class AntennaTrackerViewModel : ParameterHardwareViewModel
{
    private readonly IActiveVehicleContext active;
    private readonly SerialAntennaTracker serial;
    private readonly AntennaTrackerTestService tests;
    private readonly IUserConfirmationService confirmation;
    private readonly IPlannerSettingsService preferences;
    private CancellationTokenSource lifetime = new();

    /// <summary>Creates both tracker workflows using their dedicated protocol services.</summary>
    public AntennaTrackerViewModel(IActiveVehicleContext active, IOptionalHardwareService settings, SerialAntennaTracker serial,
        AntennaTrackerTestService tests, IUserConfirmationService confirmation, IPlannerSettingsService preferences,
        ILogger<AntennaTrackerViewModel> logger, INavigationService navigation) : base("antenna-tracker", active, settings, logger, navigation)
    {
        this.active = active; this.serial = serial; this.tests = tests; this.confirmation = confirmation; this.preferences = preferences;
        var saved = preferences.Current.SerialTracker ?? new();
        Driver = saved.Driver; HeadingZero = saved.HeadingZero; TiltOffset = saved.TiltOffset;
        PanChannel = saved.PanChannel; TiltChannel = saved.TiltChannel; Speed = saved.Speed; Acceleration = saved.Acceleration;
        Pan = new(saved.Pan); Tilt = new(saved.Tilt);
    }
    /// <summary>Gets the identity-specific workflow guidance.</summary>
    public string TargetStatus => !active.IsOnline ? "Connect an ArduPilot tracker for firmware setup, or an aircraft for standalone tracking." :
        active.State?.Identity.Firmware.Family == FirmwareFamily.AntennaTracker ? "ArduPilot AntennaTracker selected. Use Stop mode before configuration or tests." :
        "The active vehicle is not an ArduPilot tracker. It may be followed by a separately connected serial tracker.";
    /// <summary>Gets the latest movement or cleanup outcome.</summary>
    [ObservableProperty] public partial string MovementStatus { get; private set; } = "No tracker output enabled.";
    /// <summary>Gets whether movement owns the captured calibration.</summary>
    [ObservableProperty] public partial bool IsTrackerRunning { get; private set; }
    /// <summary>Gets or sets the logical tracker axis (1 yaw, 2 pitch), not a physical channel.</summary>
    [ObservableProperty] public partial int FirmwareAxis { get; set; } = 1;
    /// <summary>Gets or sets the bounded servo-test pulse in microseconds.</summary>
    [ObservableProperty] public partial int FirmwarePwm { get; set; } = 1500;
    /// <summary>Gets supported standalone protocols.</summary>
    public IReadOnlyList<SerialTrackerDriver> Drivers { get; } = Enum.GetValues<SerialTrackerDriver>();
    /// <summary>Gets or sets the serial protocol.</summary>
    [ObservableProperty] public partial SerialTrackerDriver Driver { get; set; }
    /// <summary>Gets or sets the exclusive tracker serial port.</summary>
    [ObservableProperty] public partial string Port { get; set; } = string.Empty;
    /// <summary>Gets or sets the serial baud rate.</summary>
    [ObservableProperty] public partial int Baud { get; set; } = 9600;
    /// <summary>Gets or sets the true heading corresponding to mechanical zero.</summary>
    [ObservableProperty] public partial double HeadingZero { get; set; }
    /// <summary>Gets or sets the elevation correction.</summary>
    [ObservableProperty] public partial double TiltOffset { get; set; }
    /// <summary>Gets or sets the zero-based Maestro pan channel.</summary>
    [ObservableProperty] public partial int PanChannel { get; set; }
    /// <summary>Gets or sets the zero-based Maestro tilt channel.</summary>
    [ObservableProperty] public partial int TiltChannel { get; set; } = 1;
    /// <summary>Gets or sets the Maestro speed limit.</summary>
    [ObservableProperty] public partial int Speed { get; set; } = 20;
    /// <summary>Gets or sets the Maestro acceleration limit.</summary>
    [ObservableProperty] public partial int Acceleration { get; set; } = 5;
    /// <summary>Gets pan calibration.</summary>
    public TrackerAxisEditor Pan { get; }
    /// <summary>Gets tilt calibration.</summary>
    public TrackerAxisEditor Tilt { get; }
    /// <summary>Gets or sets the physical tracker latitude.</summary>
    [ObservableProperty] public partial double HomeLatitude { get; set; }
    /// <summary>Gets or sets the physical tracker longitude.</summary>
    [ObservableProperty] public partial double HomeLongitude { get; set; }
    /// <summary>Gets or sets the physical tracker altitude above mean sea level.</summary>
    [ObservableProperty] public partial double HomeAltitude { get; set; }
    /// <summary>Gets or sets acknowledgement that the physical tracker origin has been surveyed/reviewed.</summary>
    [ObservableProperty] public partial bool HomeReviewed { get; set; }
    /// <summary>Gets or sets the test pan angle.</summary>
    [ObservableProperty] public partial double TestPan { get; set; }
    /// <summary>Gets or sets the test tilt angle.</summary>
    [ObservableProperty] public partial double TestTilt { get; set; } = 45;

    private SerialTrackerProfile Profile() => new() { Driver = Driver, Pan = Pan.Build(), Tilt = Tilt.Build(), HeadingZero = HeadingZero, TiltOffset = TiltOffset,
        PanChannel = PanChannel, TiltChannel = TiltChannel, Speed = Speed, Acceleration = Acceleration };
    /// <inheritdoc />
    protected override Task<bool> ConfirmApplyAsync(PeripheralSetting setting, double value, CancellationToken token) =>
        confirmation.ConfirmAsync("Review tracker configuration", $"{setting.Name}: {setting.CurrentValue} → {value}. Secure the tracker and select Stop mode before applying. Servo/controller changes can affect physical motion.", "Apply", token);

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task FirmwareTestAsync(CancellationToken cancellationToken)
    {
        if (IsTrackerRunning) return;
        IsTrackerRunning = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, active.ConnectionCancellationToken);
        try
        {
            var review = tests.Review(active.VehicleId ?? throw new InvalidOperationException("Connect an ArduPilot tracker."), FirmwareAxis);
            var pwm = FirmwarePwm;
            if (!await confirmation.ConfirmAsync("Move tracker servo", $"Logical {(review.Axis == 1 ? "yaw" : "pitch")} uses {review.Output}, limits {review.Minimum}–{review.Maximum} μs, trim {review.Trim} μs. Apply {pwm} μs briefly? ServoTest enables servo power. Clear the mechanism. Stop mode will be requested on completion or cancellation; loss of the connection may prevent stopping.", "Run bounded test", linked.Token)) return;
            await tests.TestAsync(review, pwm, true, new Progress<string>(text => MovementStatus = text), linked.Token);
        }
        catch (OperationCanceledException) { SetMessages("Test cancelled or timed out; check the movement status."); }
        catch (Exception ex) { MovementStatus = ex.Message; SetMessages(ex); }
        finally { IsTrackerRunning = false; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SerialTestAsync(CancellationToken token) => RunSerialAsync(false, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task TrackAsync(CancellationToken token) => RunSerialAsync(true, token);
    private async Task RunSerialAsync(bool follow, CancellationToken token)
    {
        if (IsTrackerRunning) return;
        IsTrackerRunning = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            var profile = Profile(); profile.Validate();
            var port = Port; var baud = Baud;
            var origin = new GeoPosition(HomeLatitude, HomeLongitude); var altitude = HomeAltitude;
            var pan = TestPan; var tilt = TestTilt;
            var aircraft = active.VehicleId; var boundary = active.ConnectionCancellationToken;
            if (follow && (!HomeReviewed || aircraft is null)) throw new InvalidOperationException("Review the physical tracker origin and connect the tracked aircraft first.");
            var action = follow ? $"Follow aircraft {aircraft} from fixed tracker position {origin.LatitudeDegrees:F7}, {origin.LongitudeDegrees:F7}, {altitude:0.0} m MSL" : $"Test mechanical pan {pan:0.0}°, tilt {tilt:0.0}° for two seconds";
            if (!await confirmation.ConfirmAsync("Move serial tracker", $"{action} using {profile.Driver} on {port} at {baud} baud? Review mechanical travel, heading and direction, then clear the mechanism. Angles are clamped to the saved travel; no over-the-top flip is used. Maestro pulse-disable is requested on stop; ASCII drivers retain their last position without a disable command.", "Start", linked.Token)) return;
            if (follow && (active.VehicleId != aircraft || active.ConnectionCancellationToken != boundary)) throw new InvalidOperationException("Aircraft connection changed during confirmation.");
            var progress = new Progress<string>(text => MovementStatus = text);
            if (follow) await serial.TrackAsync(port, baud, profile, origin, altitude, aircraft!.Value, true, progress, linked.Token);
            else await serial.TestAsync(port, baud, profile, pan, tilt, true, progress, linked.Token);
        }
        catch (OperationCanceledException) { SetMessages("Serial tracker stopped or timed out; check the movement status."); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsTrackerRunning = false; }
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SaveCalibrationAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            var profile = Profile(); profile.Validate();
            var result = await preferences.SaveAsync(preferences.Current with { SerialTracker = profile }, linked.Token);
            if (!result.Success) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Message)));
            SetMessages("Local tracker calibration saved. Port, origin and movement are reviewed again for each session.");
        }
        catch (OperationCanceledException) { SetMessages("Saving cancelled."); }
        catch (Exception ex) { SetMessages(ex); }
    }
    private void TargetChanged(ActiveVehicleChangedEventArgs _) => Dispatcher.DispatchAsync(() => { OnPropertyChanged(nameof(TargetStatus)); return Task.CompletedTask; })
        .SafeFireAndForget(ex => Logger.LogError(ex, "Could not refresh tracker identity."));
    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        active.Changed -= TargetChanged; active.Changed += TargetChanged;
        OnPropertyChanged(nameof(TargetStatus));
        await base.ActivateAsync();
    }
    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        active.Changed -= TargetChanged; lifetime.Cancel();
        await Task.WhenAll(new[] { FirmwareTestCommand.ExecutionTask, SerialTestCommand.ExecutionTask, TrackCommand.ExecutionTask, SaveCalibrationCommand.ExecutionTask }.OfType<Task>());
        await base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose() { active.Changed -= TargetChanged; lifetime.Cancel(); base.Dispose(); }
}

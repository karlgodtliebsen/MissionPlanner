using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.Advanced.Position;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Position;

/// <summary>Provides GPS preview before separately confirmed follow or moving-base output.</summary>
public sealed partial class OperatorPositionViewModel(OperatorNavigationService navigation, IActiveVehicleContext active,
    IUserConfirmationService confirmation, ILogger<OperatorPositionViewModel> logger) : ViewModelBase(logger)
{
    private CancellationTokenSource lifetime = new();
    private MovingRallyReview? rallyReview;
    /// <summary>Gets supported external position transports.</summary>
    public IReadOnlyList<RtkSourceKind> Sources { get; } = [RtkSourceKind.Serial, RtkSourceKind.Tcp, RtkSourceKind.Udp];
    /// <summary>Gets or sets the external position transport.</summary>
    [ObservableProperty] public partial RtkSourceKind Source { get; set; }
    /// <summary>Gets or sets the serial name, TCP host or UDP bind address.</summary>
    [ObservableProperty] public partial string Endpoint { get; set; } = string.Empty;
    /// <summary>Gets or sets the serial baud or network port.</summary>
    [ObservableProperty] public partial int PortOrBaud { get; set; } = 9600;
    /// <summary>Gets the latest position or source status.</summary>
    [ObservableProperty] public partial string PositionStatus { get; private set; } = "GPS input stopped.";
    /// <summary>Gets the last navigation outcome, including Hold failures.</summary>
    [ObservableProperty] public partial string NavigationStatus { get; private set; } = "No navigation output enabled.";
    /// <summary>Gets or sets the explicitly reviewed HOME-relative follow altitude.</summary>
    [ObservableProperty] public partial double FollowAltitude { get; set; } = 10;
    /// <summary>Gets or sets the maximum horizontal distance from vehicle to operator.</summary>
    [ObservableProperty] public partial double MaximumDistance { get; set; } = 100;
    /// <summary>Gets or sets whether Moving Base may update one reviewed rally point.</summary>
    [ObservableProperty] public partial bool UpdateRally { get; set; }
    /// <summary>Gets or sets the height above the moving GPS MSL altitude for the rally point.</summary>
    [ObservableProperty] public partial double RallyClearance { get; set; } = 100;
    /// <summary>Gets available one-based indexes from the reviewed plan.</summary>
    public ObservableCollection<int> RallyIndexes { get; } = [];
    /// <summary>Gets or sets the existing rally point to update.</summary>
    [ObservableProperty] public partial int SelectedRally { get; set; } = 1;
    /// <summary>Gets the reviewed rally plan summary.</summary>
    [ObservableProperty] public partial string RallySummary { get; private set; } = "Read the existing ArduPlane rally plan before enabling rally updates.";

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task PreviewAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            PositionStatus = "Opening GPS input; waiting for a fresh GPGGA/GNGGA fix.";
            await navigation.Input.RunAsync(new(Source, Endpoint, PortOrBaud), new Progress<OperatorPositionFix>(fix =>
                PositionStatus = fix.Valid ? $"{fix.Position.LatitudeDegrees:F7}, {fix.Position.LongitudeDegrees:F7}; MSL {fix.AltitudeMsl:0.0} m; {fix.Satellites} satellites; HDOP {fix.Hdop:0.0}; received {fix.ObservedAt:T}." : fix.Status), linked.Token);
        }
        catch (OperationCanceledException) { PositionStatus = "GPS input stopped or timed out. Restart explicitly."; }
        catch (Exception ex) { PositionStatus = "GPS input stopped: " + ex.Message; }
        finally { FollowCommand.Cancel(); MoveBaseCommand.Cancel(); }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task FollowAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, active.ConnectionCancellationToken);
        try
        {
            var id = active.VehicleId ?? throw new InvalidOperationException("Select the aircraft first.");
            var boundary = active.ConnectionCancellationToken;
            var altitude = FollowAltitude;
            var maximum = MaximumDistance;
            if (!await confirmation.ConfirmAsync("Start Follow Me", $"Vehicle {id} will follow the external GPS at {altitude:0.0} m above HOME, with a maximum target distance of {maximum:0} m. It must already be armed in Guided mode. Verify terrain clearance and pilot control. Stopping or losing the fix requests Hold if the original connection is still available; take control if Hold cannot be confirmed.", "Start following", linked.Token)) return;
            if (active.VehicleId != id || boundary != active.ConnectionCancellationToken) throw new InvalidOperationException("The target changed during confirmation.");
            await navigation.FollowAsync(id, altitude, maximum, true, new Progress<string>(text => NavigationStatus = text), linked.Token);
        }
        catch (OperationCanceledException) { SetMessages("Follow Me ended. Check the navigation status and actual vehicle mode."); }
        catch (Exception ex) { SetMessages(ex); }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ReadRallyAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, active.ConnectionCancellationToken);
        try
        {
            rallyReview = null; RallyIndexes.Clear();
            var id = active.VehicleId ?? throw new InvalidOperationException("Select an ArduPlane vehicle.");
            rallyReview = await navigation.ReadRallyAsync(id, linked.Token);
            for (var i = 1; i <= rallyReview.Plan.Points.Count; i++) RallyIndexes.Add(i);
            SelectedRally = RallyIndexes.FirstOrDefault();
            RallySummary = rallyReview.Plan.Points.Count == 0 ? "No rally points exist. Add and upload one in the planner, then read again. This tool does not replace the plan with a new single point." :
                string.Join("\n", rallyReview.Plan.Points.Select((point, i) => $"{i + 1}: {point.Position.LatitudeDegrees:F7}, {point.Position.LongitudeDegrees:F7}; {point.Altitude.Meters:0.0} m {point.Altitude.Reference}"));
        }
        catch (OperationCanceledException) { SetMessages("Rally read cancelled or timed out."); }
        catch (Exception ex) { SetMessages(ex); }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task MoveBaseAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var review = UpdateRally ? rallyReview ?? throw new InvalidOperationException("Read and review the rally plan first.") : null;
            var selected = SelectedRally;
            var clearance = RallyClearance;
            if (!await confirmation.ConfirmAsync("Start Moving Base", review is null ? "Update the session-local tracker home from external GPS once per second? Vehicle HOME and rally points will not be changed." :
                $"Update existing rally point {selected} on vehicle {review.Session.Id} to the GPS location with MSL altitude plus {clearance:0.0} m? Updates are at most every five seconds and can change RTL destinations while flying. Other rally points are preserved. Stopping leaves the last uploaded point in place.", "Start moving base", linked.Token)) return;
            await navigation.MoveBaseAsync(review, selected - 1, clearance, true, new Progress<string>(text => NavigationStatus = text), linked.Token);
        }
        catch (OperationCanceledException) { SetMessages("Moving Base stopped. The last local home and any uploaded rally point remain at their last values."); }
        catch (Exception ex) { SetMessages(ex); }
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        return base.ActivateAsync();
    }
    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel();
        await Task.WhenAll(new[] { PreviewCommand.ExecutionTask, FollowCommand.ExecutionTask, MoveBaseCommand.ExecutionTask, ReadRallyCommand.ExecutionTask }.OfType<Task>());
        rallyReview = null; RallyIndexes.Clear();
        await base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose() { lifetime.Cancel(); base.Dispose(); }
}

using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Arming;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Setup.Arming;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises local staging, lifecycle boundaries and typed command presentation.</summary>
public sealed class ArmingViewModelTests
{
    /// <summary>Edits/defaults/discard are local, and verified Apply clears pending state.</summary>
    [Fact]
    public async Task PendingDefaultDiscardApply()
    {
        using var f = new Fixture(); await f.Model.ActivateAsync();
        f.Edit(0); Assert.True(f.Model.HasPendingChanges); Assert.True(f.Model.CanApply);
        await f.Config.DidNotReceive().ApplyAsync(f.Id, Arg.Any<ArmingChangeSet>(), Arg.Any<CancellationToken>());
        f.Model.Settings[0].ResetDefaultCommand.Execute(null); Assert.False(f.Model.HasPendingChanges);
        f.Edit(0); f.Model.DiscardCommand.Execute(null); Assert.False(f.Model.HasPendingChanges);
        f.Edit(0); await f.Model.ApplyCommand.ExecuteAsync(null);
        Assert.False(f.Model.HasPendingChanges);
        Assert.True(f.Model.RequiresReboot);
        await f.Config.Received(1).ApplyAsync(f.Id, Arg.Any<ArmingChangeSet>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Parameter completion refreshes capability; unchanged evidence retains the same documents.</summary>
    [Fact]
    public async Task LoadingAndStableDocuments()
    {
        using var f = new Fixture(); f.Setup = f.Setup with { ParametersReady = false };
        await f.Model.ActivateAsync(); Assert.False(f.Model.CanEdit);
        f.Setup = f.Setup with { ParametersReady = true };
        await f.Model.RefreshCommand.ExecuteAsync(null); Assert.True(f.Model.CanEdit);
        var original = f.Model.StatusDocument;
        await f.Model.RefreshCommand.ExecuteAsync(null); Assert.Same(original, f.Model.StatusDocument);
        await f.Model.DeactivateAsync(); Assert.Empty(f.Model.Settings); Assert.False(f.Model.CanArm);
        await f.Model.ActivateAsync(); Assert.NotEmpty(f.Model.Settings);
    }

    /// <summary>Readback changes beneath pending edits invalidate Apply.</summary>
    [Fact]
    public async Task ConflictingReadback()
    {
        using var f = new Fixture(); await f.Model.ActivateAsync(); f.Edit(0);
        f.Setup = f.Setup with { Current = f.Setup.Current with { Stick = StickArmingMode.Disabled } };
        await f.Model.RefreshCommand.ExecuteAsync(null);
        Assert.True(f.Model.HasConflict); Assert.False(f.Model.CanApply);
        f.Model.DiscardCommand.Execute(null); Assert.False(f.Model.HasConflict);
    }

    /// <summary>ACK outcomes never directly set Armed; policy and explicit confirmation are respected.</summary>
    [Theory]
    [InlineData(VehicleCommandResult.Accepted)]
    [InlineData(VehicleCommandResult.Denied)]
    [InlineData(VehicleCommandResult.Timeout)]
    public async Task ArmOutcomes(VehicleCommandResult result)
    {
        using var f = new Fixture();
        f.Commands.ArmAsync(f.Id, Arg.Any<CancellationToken>()).Returns(new VehicleCommandResponse(f.Id, result, DateTimeOffset.UnixEpoch));
        await f.Model.ActivateAsync(); await f.Model.ArmCommand.ExecuteAsync(null);
        await f.Confirmation.Received(1).ConfirmAsync("Arm vehicle?", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains(result.ToString(), f.Model.OperationMessage);
        Assert.DoesNotContain("Armed heartbeat observed", f.Model.StatusDocument!.Markdown);
    }

    /// <summary>Cancelled or denied confirmation prevents transmission.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NoSendWhenDeniedOrCancelled(bool denied)
    {
        using var f = new Fixture();
        if (denied) { f.Policy.Evaluate(Arg.Any<VehicleState>(), VehicleAction.Arm).Returns(VehicleCommandDecision.Deny("stale heartbeat")); }
        else { f.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false); }
        await f.Model.ActivateAsync(); await f.Model.ArmCommand.ExecuteAsync(null);
        await f.Commands.DidNotReceive().ArmAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Hazardous disarm passes explicit safety confirmation through the typed service.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisarmPolicy(bool hazardous)
    {
        using var f = new Fixture();
        f.Policy.Evaluate(Arg.Any<VehicleState>(), VehicleAction.Disarm).Returns(VehicleCommandDecision.Allow(hazardous, "Not confirmed on ground"));
        f.Commands.DisarmAsync(f.Id, hazardous, Arg.Any<CancellationToken>()).Returns(new VehicleCommandResponse(f.Id, VehicleCommandResult.Accepted, DateTimeOffset.UnixEpoch));
        await f.Model.ActivateAsync(); await f.Model.DisarmCommand.ExecuteAsync(null);
        await f.Commands.Received(1).DisarmAsync(f.Id, hazardous, Arg.Any<CancellationToken>());
    }

    /// <summary>Selection changes during confirmation cancel the command and discard old pending state.</summary>
    [Fact]
    public async Task VehicleSwitchDuringConfirmation()
    {
        using var f = new Fixture(); var answer = new TaskCompletionSource<bool>();
        f.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(answer.Task);
        await f.Model.ActivateAsync(); f.Edit(0);
        var request = f.Model.ArmCommand.ExecuteAsync(null);
        f.Active.VehicleId.Returns(new VehicleId(2, 1)); f.Active.IsOnline.Returns(false);
        f.Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(f.Active.Current, f.Active.Current));
        answer.SetResult(true); await request;
        Assert.False(f.Model.HasPendingChanges); Assert.Empty(f.Model.Settings);
        Assert.Contains("Disconnected", f.Model.StatusDocument!.Markdown);
        await f.Commands.DidNotReceive().ArmAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Replay prohibits both configuration writes and vehicle actions.</summary>
    [Fact]
    public async Task ReplayAndDisconnected()
    {
        using var f = new Fixture();
        f.Active.IsOnline.Returns(false); await f.Model.ActivateAsync(); Assert.False(f.Model.CanArm);
        f.Active.IsOnline.Returns(true);
        f.Replay.Snapshot.Returns(ReplaySessionSnapshot.Unloaded with { State = ReplaySessionState.Paused });
        await f.Model.RefreshCommand.ExecuteAsync(null);
        Assert.False(f.Model.CanEdit); Assert.False(f.Model.CanArm); Assert.False(f.Model.CanDisarm);
    }

    /// <summary>Custom metadata checks stage a mask, never the special All bit or a sentinel.</summary>
    [Fact]
    public async Task CustomCheckSelection()
    {
        using var f = new Fixture(); await f.Model.ActivateAsync();
        f.Edit(-1); Assert.True(f.Model.Settings[0].IsCustom);
        f.Model.Settings[0].Bits[0].IsChecked = true;
        Assert.Contains(f.Model.PendingChanges, c => c.Contains("1 → 2"));
        await f.Config.DidNotReceive().ApplyAsync(f.Id, Arg.Any<ArmingChangeSet>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Fresh radio endpoint movement is reused without claiming successful arming.</summary>
    [Fact]
    public async Task SwitchMovement()
    {
        using var f = new Fixture(); await f.Model.ActivateAsync();
        f.Model.SelectedSwitch = f.Model.SwitchChoices.Single(c => c.Value == 8);
        foreach (var pwm in new ushort[] { 999, 2000, 999 })
        {
            var radio = new VehicleRadioState(8, Enumerable.Repeat((ushort)1500, 7).Append(pwm).ToArray(), 100, DateTimeOffset.UtcNow);
            f.Active.State.Returns(f.Active.State! with { Radio = radio });
            await f.Model.RefreshCommand.ExecuteAsync(null);
        }
        Assert.Contains("999 → 2000 → 999", f.Model.Movement);
        Assert.DoesNotContain("Armed heartbeat observed", f.Model.StatusDocument!.Markdown);
    }

    /// <summary>Only new diagnostic heartbeat evidence changes the status to Armed.</summary>
    [Fact]
    public async Task HeartbeatCompletesObservedState()
    {
        using var f = new Fixture();
        f.Commands.ArmAsync(f.Id, Arg.Any<CancellationToken>()).Returns(new VehicleCommandResponse(f.Id, VehicleCommandResult.Accepted, DateTimeOffset.UnixEpoch));
        await f.Model.ActivateAsync(); await f.Model.ArmCommand.ExecuteAsync(null);
        Assert.DoesNotContain("Armed heartbeat observed", f.Model.StatusDocument!.Markdown);
        f.Live.GetArming(f.Id).Returns(new VehicleArmingDiagnostic("ARMED", true, null, [], null, DateTimeOffset.UnixEpoch, "Accepted") { Stage = ArmingDiagnosticStage.Armed });
        await f.Model.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("Armed heartbeat observed", f.Model.StatusDocument!.Markdown);
    }

    /// <summary>An unverified apply retains the pending values for another explicit review.</summary>
    [Fact]
    public async Task FailedApplyRetainsEdits()
    {
        using var f = new Fixture();
        f.Config.ApplyAsync(f.Id, Arg.Any<ArmingChangeSet>(), Arg.Any<CancellationToken>()).Returns(_ => new ArmingApplyResult(false, f.Setup, [], false, "Readback mismatch"));
        await f.Model.ActivateAsync(); f.Edit(0); await f.Model.ApplyCommand.ExecuteAsync(null);
        Assert.True(f.Model.HasPendingChanges);
        Assert.Contains("Readback mismatch", f.Model.OperationMessage);
    }

    /// <summary>A late load cannot resurrect a hidden page or create a new polling lifetime.</summary>
    [Fact]
    public async Task DeactivateDuringLoad()
    {
        using var f = new Fixture(); var completion = new TaskCompletionSource<ArmingSetupState>();
        f.Config.ReadAsync(f.Id, Arg.Any<CancellationToken>()).Returns(completion.Task);
        var activation = f.Model.ActivateAsync();
        await f.Model.DeactivateAsync();
        completion.SetResult(f.Setup); await activation;
        Assert.Empty(f.Model.Settings); Assert.False(f.Model.CanEdit);
        Assert.Contains("Disconnected", f.Model.StatusDocument!.Markdown);
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly VehicleId Id = new(1, 1);
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly IArmingConfigurationService Config = Substitute.For<IArmingConfigurationService>();
        internal readonly IVehicleCommandService Commands = Substitute.For<IVehicleCommandService>();
        internal readonly IVehicleCommandPolicy Policy = Substitute.For<IVehicleCommandPolicy>();
        internal readonly IUserConfirmationService Confirmation = Substitute.For<IUserConfirmationService>();
        internal readonly IVehicleLiveDiagnostics Live = Substitute.For<IVehicleLiveDiagnostics>();
        internal readonly IReplaySessionManager Replay = Substitute.For<IReplaySessionManager>();
        internal ArmingSetupState Setup;
        internal readonly ArmingViewModel Model;
        internal Fixture()
        {
            var vehicle = new VehicleState(Id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
                DateTimeOffset.UtcNow, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            Active.VehicleId.Returns(Id); Active.IsOnline.Returns(true); Active.State.Returns(vehicle);
            Replay.Snapshot.Returns(ReplaySessionSnapshot.Unloaded);
            Policy.Evaluate(Arg.Any<VehicleState>(), Arg.Any<VehicleAction>()).Returns(VehicleCommandDecision.Allow());
            Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            Setup = new(Id, new(PreArmCheckMode.All),
                [new(ArmingSetting.Checks, "Checks", "ARMING_CHECK", 1, 1, [new(1, "All"), new(0, "Disabled"), new(-1, "Custom")], [new(2, "Barometer")], true, true, null)],
                [], true, true, true, new Dictionary<string, double> { ["ARMING_CHECK"] = 1 }, "Loaded");
            Config.ReadAsync(Id, Arg.Any<CancellationToken>()).Returns(_ => Setup);
            Config.EvaluateChangesAsync(Id, Arg.Any<ArmingConfiguration>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var desired = call.Arg<ArmingConfiguration>()!;
                return new ArmingChangeSet(new(Id, vehicle.Identity.Firmware), Active.ConnectionCancellationToken, Setup.Current, desired,
                    desired == Setup.Current ? [] : [new("ARMING_CHECK", Setup.Current.Get(ArmingSetting.Checks)!.Value, desired.Get(ArmingSetting.Checks)!.Value, true)], [], []);
            });
            Config.ApplyAsync(Id, Arg.Any<ArmingChangeSet>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Setup = Setup with { Current = call.Arg<ArmingChangeSet>()!.Desired };
                return new ArmingApplyResult(true, Setup, ["ARMING_CHECK"], true, "Verified");
            });
            var live = Live;
            live.GetArming(Id).Returns(new VehicleArmingDiagnostic("DISARMED / READY", false, true, [], null, null, null) { Stage = ArmingDiagnosticStage.DisarmedReady });
            var dispatcher = Substitute.For<IUiDispatcher>();
            dispatcher.CheckAccess().Returns(true);
            dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(c => c.Arg<Action>()!());
            dispatcher.DispatchAsync(Arg.Any<Action>()).Returns(c => { c.Arg<Action>()!(); return Task.CompletedTask; });
            dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(c => c.Arg<Func<Task>>()!());
            Model = new(Active, Config, live, Commands, Policy, Confirmation, new ArmingSetupDocumentFactory(),
                Substitute.For<INavigationService>(), Replay, TimeProvider.System, NullLogger<ArmingViewModel>.Instance, dispatcher, Substitute.For<IDomainEventHub>());
        }
        internal void Edit(int value) => Model.Settings[0].Selected = Model.Settings[0].Choices.Single(c => c.Value == value);
        public void Dispose() => Model.Dispose();
    }
}

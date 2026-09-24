using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Tests local Compass staging, readback outcomes and connection safety.</summary>
public sealed class CompassSetupViewModelTests
{
    /// <summary>Edits/default reset/discard are local; only successful Apply clears pending values.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingWorkflow(bool failure)
    {
        using var f = new Fixture { Failure = failure };
        await f.Model.LoadAsync();
        Assert.False(f.Model.CanStart);
        f.Edit(CompassSetting.Enabled, 1);
        Assert.True(f.Model.HasPendingChanges);
        Assert.True(f.Model.CanApply);
        Assert.Equal(0, f.Writes);
        f.Model.DiscardCommand.Execute(null);
        Assert.False(f.Model.HasPendingChanges);
        f.Edit(CompassSetting.PrimaryOrientation, 1);
        f.Row(CompassSetting.PrimaryOrientation).ResetDefaultCommand.Execute(null);
        Assert.False(f.Model.HasPendingChanges);
        f.Edit(CompassSetting.Enabled, 1);
        f.Edit(CompassSetting.PrimaryOrientation, 1);
        await f.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Equal(1, f.Writes);
        Assert.Equal(failure, f.Model.HasPendingChanges);
        Assert.Equal(!failure, f.Model.RequiresReboot);
        Assert.False(f.Model.CanStart);
    }

    /// <summary>Changed live values after reconnect invalidate a pending review.</summary>
    [Fact]
    public async Task ReconnectConflict()
    {
        using var f = new Fixture();
        await f.Model.LoadAsync();
        f.Edit(CompassSetting.Enabled, 1);
        f.Active.IsOnline.Returns(false);
        await f.Model.LoadAsync();
        Assert.False(f.Model.CanApply);
        Assert.True(f.Model.HasPendingChanges);
        f.Active.IsOnline.Returns(true);
        f.State = f.State with { Current = f.State.Current with { PrimaryOrientation = 1 } };
        await f.Model.LoadAsync();
        Assert.True(f.Model.HasConflict);
        Assert.False(f.Model.CanApply);
        f.Model.DiscardCommand.Execute(null);
        Assert.False(f.Model.HasConflict);
        Assert.False(f.Model.HasPendingChanges);
    }

    /// <summary>Loading and unsupported firmware expose no writable guessed defaults.</summary>
    [Fact]
    public async Task LoadingAndCapability()
    {
        using var f = new Fixture();
        f.Load.Get(f.Id).Returns(new ParameterLoadStatus(f.Id, ParameterLoadState.Downloading, 1, 20, 5, "Loading", DateTimeOffset.UtcNow));
        await f.Model.LoadAsync();
        Assert.Empty(f.Model.Settings);
        Assert.False(f.Model.CanApply);
        f.Load.Get(f.Id).Returns((ParameterLoadStatus?)null);
        await f.Model.LoadAsync();
        Assert.NotEmpty(f.Model.Settings);
        f.State = f.State with { IsSupported = false, UnsupportedReason = "Unsupported" };
        await f.Model.LoadAsync();
        Assert.False(f.Model.CanEdit);
        Assert.False(f.Model.CanStart);
    }

    /// <summary>The existing calibration service receives commands only with confirmed enabled state.</summary>
    [Fact]
    public async Task CalibrationIsPreserved()
    {
        using var f = new Fixture();
        f.State = f.State with { Current = f.State.Current with { Enabled = true } };
        await f.Model.LoadAsync();
        Assert.True(f.Model.CanStart);
        await f.Model.StartCalibrationCommand.ExecuteAsync(null);
        await f.Calibration.Received(1).StartAsync(f.Id, false, Arg.Any<CancellationToken>());
        Assert.Equal(0, f.Writes);
    }

    /// <summary>Repeated registry projections preserve the report while relevant changes replace it.</summary>
    [Fact]
    public async Task DocumentRefreshesOnlyForRelevantChanges()
    {
        using var f = new Fixture();
        await f.Model.LoadAsync();
        var original = f.Model.StatusDocument;
        f.State = f.State with { ObservedAt = DateTimeOffset.UtcNow.AddSeconds(1), Diagnostics = ["unrelated timestamp"] };
        await f.Model.LoadAsync();
        Assert.Same(original, f.Model.StatusDocument);
        f.State = f.State with { Health = "Unhealthy" };
        await f.Model.LoadAsync();
        Assert.NotSame(original, f.Model.StatusDocument);
        f.Edit(CompassSetting.Enabled, 1);
        Assert.Contains("Pending changes", f.Model.StatusDocument!.Markdown);
        f.Model.DiscardCommand.Execute(null);
        Assert.DoesNotContain("Pending changes", f.Model.StatusDocument!.Markdown);
        f.Active.IsOnline.Returns(false);
        await f.Model.LoadAsync();
        Assert.Contains("Disconnected", f.Model.StatusDocument!.Markdown);
        f.Active.IsOnline.Returns(true);
        await f.Model.LoadAsync();
        Assert.DoesNotContain("Disconnected", f.Model.StatusDocument!.Markdown);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly VehicleId Id = new(1, 1);
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly IVehicleParameterLoadStatusContext Load = Substitute.For<IVehicleParameterLoadStatusContext>();
        internal readonly IArduPilotCompassCalibrationService Calibration = Substitute.For<IArduPilotCompassCalibrationService>();
        internal readonly CompassSetupViewModel Model;
        internal CompassSetupState State;
        internal bool Failure;
        internal int Writes;

        internal Fixture()
        {
            var vehicle = new VehicleState(Id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
                DateTimeOffset.UtcNow, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            Active.VehicleId.Returns(Id);
            Active.IsOnline.Returns(true);
            Active.State.Returns(vehicle);
            CompassSettingDefinition[] definitions =
            [
                new(CompassSetting.Enabled, "Enable", "Magnetic heading", "COMPASS_ENABLE", 0, null, [new(0,"Off"),new(1,"On")], true, true, null),
                new(CompassSetting.PrimaryOrientation, "Orientation", "Mounting", "COMPASS_ORIENT", 0, 0, [new(0,"None"),new(1,"Yaw 45")], true, false, null)
            ];
            State = new(Id, new() { Enabled = false, PrimaryOrientation = 0 }, definitions, [], "Disabled", "Valid", "Unknown", true, null, DateTimeOffset.UtcNow, []);
            var service = Substitute.For<ICompassConfigurationService>();
            service.ReadAsync(Id, Arg.Any<CancellationToken>()).Returns(_ => State);
            service.EvaluateChangesAsync(Id, Arg.Any<CompassConfiguration>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var desired = call.Arg<CompassConfiguration>()!;
                return new CompassChangeSet(new(Id, vehicle.Identity.Firmware), Active.ConnectionCancellationToken, State.Current, desired,
                    definitions.Where(d => State.Current.Get(d.Setting) != desired.Get(d.Setting))
                        .Select(d => new CompassParameterChange(d.ParameterName, State.Current.Get(d.Setting)!.Value, desired.Get(d.Setting)!.Value, d.RequiresReboot, d.Label)).ToArray(), [], []);
            });
            service.ApplyAsync(Id, Arg.Any<CompassChangeSet>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Writes++;
                var changes = call.Arg<CompassChangeSet>()!;
                if (!Failure)
                {
                    State = State with { Current = changes.Desired, Settings = definitions.Select(d => d with { Current = changes.Desired.Get(d.Setting) }).ToArray() };
                }
                return new CompassApplyResult(!Failure, State, !Failure && changes.RequiresReboot, [], Failure ? "Mismatch" : "Applied");
            });
            var dispatcher = Substitute.For<IUiDispatcher>();
            dispatcher.CheckAccess().Returns(true);
            dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
            dispatcher.DispatchAsync(Arg.Any<Action>()).Returns(call => { call.Arg<Action>()!(); return Task.CompletedTask; });
            var confirmation = Substitute.For<IUserConfirmationService>();
            confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            Calibration.Current.Returns(CompassCalibrationSnapshot.Initial);
            Model = new(Active, service, Calibration, Substitute.For<IVehicleParameterRegistry>(), Substitute.For<ISetupCompletionStore>(),
                Substitute.For<ISetupWorkflowCatalog>(), confirmation, Substitute.For<IDateTimeProvider>(), NullLogger<CompassSetupViewModel>.Instance,
                dispatcher, Substitute.For<IDomainEventHub>(), Load, Substitute.For<IVehicleCommandService>(), Substitute.For<INavigationService>(),
                new MissionPlanner.App.Presentation.Documents.CompassSetupDocumentFactory());
        }
        internal CompassSettingViewModel Row(CompassSetting key) => Model.Settings.Single(s => s.Definition.Setting == key);
        internal void Edit(CompassSetting key, int value) => Row(key).Selected = Row(key).Choices.Single(c => c.Value == value);
        public void Dispose() => Model.Dispose();
    }
}

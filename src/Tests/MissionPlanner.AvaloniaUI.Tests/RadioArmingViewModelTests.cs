using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Regresses RC5's contradictory assigned/conflict presentation.</summary>
public sealed class RadioArmingViewModelTests
{
    /// <summary>Movement, assignment, conflict, and confirmation remain distinct for all three option cases.</summary>
    [Theory]
    [InlineData(153)]
    [InlineData(7)]
    [InlineData(0)]
    public async Task Rc5AssignmentMatchesDisplayedDiagnostic(float option)
    {
        var id = new VehicleId(1, 1);
        var now = DateTimeOffset.UtcNow;
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(id);
        active.IsOnline.Returns(true);
        var registry = new VehicleParameterRegistry();
        void Store(string name, float value) => registry.StoreParameter(id,
            new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);
        Store("FLTMODE_CH", 8);
        Store("RC5_OPTION", option);
        Store("RCMAP_ROLL", 1);
        Store("RCMAP_PITCH", 2);
        Store("RCMAP_THROTTLE", 3);
        Store("RCMAP_YAW", 4);
        var factory = Substitute.For<IDomainFactory>();
        var confirmation = Substitute.For<IUserConfirmationService>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(value => value.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
        using var model = new RadioSetupViewModel(active, Substitute.For<IRadioCalibrationService>(),
            Substitute.For<IDomainEventHub>(), registry, Substitute.For<ISetupCompletionStore>(),
            Substitute.For<ISetupWorkflowCatalog>(), confirmation, Substitute.For<INavigationService>(), clock,
            Substitute.For<IVehicleCommandService>(), Substitute.For<IVehicleTelemetryEventHub>(),
            dispatcher, new RadioArmingConfiguration(active, registry, factory),
            NullLogger<RadioSetupViewModel>.Instance);
        foreach (ushort pwm in new ushort[] { 999, 2000, 999 })
        {
            var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
                VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            active.State.Returns(state with
            {
                Radio = VehicleRadioState.Empty with
                {
                    ChannelsRaw = new ushort[] { 1500, 1500, 1000, 1500, pwm },
                    ChannelCount = 5,
                    ObservedAt = now
                }
            });
            model.SelectedArmingChannel = 6;
            model.SelectedArmingChannel = 5;
        }
        Assert.Contains("999 → 2000 → 999", model.ArmingSwitchDiagnostic);
        Assert.Contains("heartbeat confirms armed state", model.ArmingSwitchDiagnostic);
        Assert.Contains("active capture window", model.ArmingSwitchDiagnostic);
        Assert.False(active.State!.IsArmed);
        if (option == 153)
        {
            Assert.Contains("Arm function: Assigned", model.ArmingSwitchDiagnostic);
            Assert.Contains("already configured", model.ArmingSwitchDiagnostic);
            Assert.DoesNotContain("Choose a free channel", model.ArmingSwitchDiagnostic);
            Assert.DoesNotContain("This channel is free", model.ArmingSwitchDiagnostic);
        }
        else if (option != 0)
        {
            Assert.Contains("auxiliary function 7", model.ArmingSwitchDiagnostic);
        }
        else
        {
            Assert.Contains("This channel is free", model.ArmingSwitchDiagnostic);
        }
        await model.AssignArmSwitchCommand.ExecuteAsync(null);
        Assert.Equal(option == 0 ? 1 : 0, confirmation.ReceivedCalls().Count());
        Assert.Empty(factory.ReceivedCalls());
        if (option == 153)
        {
            Assert.Contains("already configured", model.StatusMessage);
            Assert.True(string.IsNullOrEmpty(model.ErrorMessage));
        }
    }
}

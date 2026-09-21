using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks receiver button gating and truthful ACK presentation in the actual Radio view model.</summary>
public sealed class ReceiverBindViewModelTests
{
    /// <summary>Binding requires an online vehicle and permission from the shared safety/capability policy.</summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void AvailabilityControlsButton(bool online, bool allowed, bool expected)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.IsOnline.Returns(online);
        active.VehicleId.Returns(new VehicleId(1, 1));
        var commands = Substitute.For<IVehicleCommandService>();
        commands.GetReceiverBindAvailability(new(1, 1)).Returns(new ReceiverBindAvailability(allowed, "CRSF", "Expected", allowed ? "Available" : "Armed"));
        using var model = Create(active, commands);
        Assert.Equal(expected, model.BindReceiverCommand.CanExecute(null));
        Assert.Contains(allowed ? "Available" : "Armed", model.BindAvailability);
    }

    /// <summary>The ACK outcome appears without claiming physical binding or starting another command.</summary>
    [Theory]
    [InlineData(VehicleCommandResult.Unsupported, ReceiverBindState.Unsupported)]
    [InlineData(VehicleCommandResult.Failed, ReceiverBindState.Failed)]
    [InlineData(VehicleCommandResult.Accepted, ReceiverBindState.WaitingForLink)]
    public async Task AckOutcomeIsShownAndCorrelated(VehicleCommandResult ack, ReceiverBindState expected)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.IsOnline.Returns(true);
        active.VehicleId.Returns(new VehicleId(1, 1));
        var commands = Substitute.For<IVehicleCommandService>();
        commands.GetReceiverBindAvailability(new(1, 1)).Returns(new ReceiverBindAvailability(true, "CRSF", "Expected", "Available"));
        var correlation = Guid.NewGuid();
        commands.StartReceiverBindAsync(new(1, 1), Arg.Any<CancellationToken>())
            .Returns(new ReceiverBindResult(new(new(1, 1), ack, DateTimeOffset.UtcNow), correlation));
        var telemetry = Substitute.For<IVehicleTelemetryEventHub>();
        using var model = Create(active, commands, telemetry);
        // No active state ends the post-ACK observation immediately, without inventing link recovery.
        await model.BindReceiverCommand.ExecuteAsync(null);
        Assert.Equal(expected, model.BindState);
        Assert.Contains(telemetry.ReceivedCalls().SelectMany(c => c.GetArguments())
            .OfType<MissionPlanner.Core.Diagnostics.VehicleDiagnosticEvent>(), e => e.CorrelationId == correlation);
        if (ack == VehicleCommandResult.Accepted)
        {
            Assert.Contains("accepted", model.BindMessage);
        }
        Assert.Single(commands.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IVehicleCommandService.StartReceiverBindAsync));
    }

    private static RadioSetupViewModel Create(IActiveVehicleContext active, IVehicleCommandService commands,
        IVehicleTelemetryEventHub? telemetry = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return new(active, Substitute.For<IRadioCalibrationService>(), Substitute.For<IDomainEventHub>(),
            Substitute.For<IVehicleParameterRegistry>(), Substitute.For<ISetupCompletionStore>(),
            Substitute.For<ISetupWorkflowCatalog>(), Substitute.For<IUserConfirmationService>(), clock,
            NullLogger<RadioSetupViewModel>.Instance, commands, telemetry ?? Substitute.For<IVehicleTelemetryEventHub>(),
            Substitute.For<IUiDispatcher>());
    }
}

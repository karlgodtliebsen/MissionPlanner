using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class FrameSetupViewModelTests
{
    [Fact]
    public async Task ParameterCompletionReloadsActivePageAndUnsubscribesOnExit()
    {
        var vehicleId = new VehicleId(1, 1);
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(vehicleId);
        active.IsOnline.Returns(true);
        var service = Substitute.For<IFrameConfigurationService>();
        var configuration = new FrameConfigurationSnapshot(vehicleId, FirmwareFamily.Unknown, [], []);
        service.GetConfigurationAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(configuration));
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
        dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(call => call.Arg<Func<Task>>()!());
        var events = Substitute.For<IDomainEventHub>();
        var subscription = Substitute.For<IDisposable>();
        Func<VehicleParameterLoadStatusChanged, CancellationToken, Task> handler = null!;
        events.SubscribeDomainEventAsync(Arg.Any<Func<VehicleParameterLoadStatusChanged, CancellationToken, Task>>())
            .Returns(call =>
            {
                handler = call.Arg<Func<VehicleParameterLoadStatusChanged, CancellationToken, Task>>()!;
                return subscription;
            });
        using var model = new FrameSetupViewModel(active, service, Substitute.For<IVehicleParameterRegistry>(),
            Substitute.For<ISetupCompletionStore>(), Substitute.For<ISetupWorkflowCatalog>(),
            Substitute.For<IUserConfirmationService>(), Substitute.For<IDateTimeProvider>(),
            NullLogger<FrameSetupViewModel>.Instance, dispatcher, events);

        await model.ActivateAsync();
        Assert.False(model.HasSettings);
        configuration = new FrameConfigurationSnapshot(vehicleId, FirmwareFamily.ArduCopter,
            [new FrameParameterSetting("FRAME_CLASS", "Frame class", 1, MavParamType.Int32, true, [])], []);

        Task Publish(VehicleId id, ParameterLoadState state) => handler(new VehicleParameterLoadStatusChanged(
            new ParameterLoadStatus(id, state, 1, 1, 100, "Loaded", DateTimeOffset.UtcNow)), CancellationToken.None);

        await Publish(vehicleId, ParameterLoadState.Downloading);
        await Publish(new VehicleId(2, 1), ParameterLoadState.Completed);
        Assert.False(model.HasSettings);
        await Publish(vehicleId, ParameterLoadState.Completed);
        Assert.True(model.HasSettings);
        Assert.Equal("FRAME_CLASS", Assert.Single(model.Settings).Name);
        Assert.Equal("ArduCopter", model.FirmwareFamily);
        await service.Received(2).GetConfigurationAsync(vehicleId, Arg.Any<CancellationToken>());

        await model.DeactivateAsync();
        subscription.Received(1).Dispose();
        await Publish(vehicleId, ParameterLoadState.Completed);
        await service.Received(2).GetConfigurationAsync(vehicleId, Arg.Any<CancellationToken>());
    }
}

using MissionPlanner.App.Views.Navigation;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Utilities.Dialogs;
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
    [Theory]
    [InlineData(ParameterLoadState.Completed)]
    [InlineData(ParameterLoadState.Failed)]
    [InlineData(ParameterLoadState.Cancelled)]
    public async Task ParameterLoadingShowsProgressUntilTerminalStatus(ParameterLoadState terminal)
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
        var statuses = new MissionPlanner.Core.Vehicles.VehicleParameterLoadStatusContext();
        statuses.Update(new ParameterLoadStatus(vehicleId, ParameterLoadState.Starting, 0, 100, 0, "Starting download", DateTimeOffset.UtcNow));
        var dialogs = Substitute.For<IDialogService>();
        var handle = Substitute.For<IDisposable>();
        Func<string> message = null!;
        dialogs.DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                message = call.Arg<Func<string>>()!;
                Assert.Equal("Loading parameters", call.Arg<DialogOptions>()!.Title);
                return Task.FromResult(handle);
            });
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
            NullLogger<FrameSetupViewModel>.Instance, dispatcher, events, statuses, dialogs, Substitute.For<INavigationService>());

        await model.ActivateAsync();
        Assert.False(model.HasSettings);
        Assert.Equal("Starting download", message());
        await service.DidNotReceive().GetConfigurationAsync(vehicleId, Arg.Any<CancellationToken>());
        configuration = new FrameConfigurationSnapshot(vehicleId, FirmwareFamily.ArduCopter,
            [new FrameParameterSetting("FRAME_CLASS", "Frame class", 1, MavParamType.Int32, true, [])], []);

        Task Publish(VehicleId id, ParameterLoadState state)
        {
            var status = new ParameterLoadStatus(id, state, 50, 100, 50, "50 / 100", DateTimeOffset.UtcNow);
            statuses.Update(status);
            return handler(new VehicleParameterLoadStatusChanged(status), CancellationToken.None);
        }

        await Publish(vehicleId, ParameterLoadState.Downloading);
        await Publish(new VehicleId(2, 1), ParameterLoadState.Completed);
        Assert.False(model.HasSettings);
        Assert.Equal("50 / 100", message());
        handle.DidNotReceive().Dispose();
        await dialogs.Received(1).DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(), Arg.Any<CancellationToken>());
        var metadata = new TaskCompletionSource<FrameConfigurationSnapshot>();
        service.GetConfigurationAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(metadata.Task);
        var finish = Publish(vehicleId, terminal);
        if (terminal == ParameterLoadState.Completed)
        {
            Assert.False(finish.IsCompleted);
            Assert.Equal("Loading frame configuration...", message());
            handle.DidNotReceive().Dispose();
            metadata.SetResult(configuration);
        }
        await finish;
        Assert.Equal(terminal == ParameterLoadState.Completed, model.HasSettings);
        handle.Received(1).Dispose();

        // A new download is closed when navigating away, and late events cannot reopen it.
        await Publish(vehicleId, ParameterLoadState.Starting);

        await model.DeactivateAsync();
        subscription.Received(1).Dispose();
        handle.Received(2).Dispose();
        await Publish(vehicleId, ParameterLoadState.Completed);
        await service.Received(terminal == ParameterLoadState.Completed ? 1 : 0)
            .GetConfigurationAsync(vehicleId, Arg.Any<CancellationToken>());
    }
}

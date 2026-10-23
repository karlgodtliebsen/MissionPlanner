using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class PX4FlowViewModelTests
{
    [Fact]
    public async Task FocusRequestsTargetSensorAndLeavingRestoresNormalMode()
    {
        var active = Substitute.For<IActiveVehicleContext>();
        var id = new VehicleId(42, 50);
        active.VehicleId.Returns(id);
        active.IsOnline.Returns(true);
        var parameters = Substitute.For<IVehicleParameterService>();
        parameters.SetParameterAsync(id, "VIDEO_ONLY", Arg.Any<float>(), MavParamType.Int32, Arg.Any<CancellationToken>()).Returns(true);
        var registry = Substitute.For<IVehicleParameterRegistry>();
        registry.GetParameter(id, "VIDEO_ONLY").Returns(new VehicleParameter("VIDEO_ONLY", 0, MavParamType.Int32, 0, 1));
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(x => x.Dispatch(Arg.Any<Action>())).Do(x => x.Arg<Action>()());
        var model = new PX4FlowViewModel(active, parameters, registry, Substitute.For<IEventHub>(),
            NullLogger<PX4FlowViewModel>.Instance, dispatcher, Substitute.For<IDomainEventHub>());
        await model.ActivateAsync();
        Assert.True(model.CanFocus);
        await model.ToggleFocusCommand.ExecuteAsync(null);
        Assert.True(model.IsFocusMode);
        await parameters.Received(1).SetParameterAsync(id, "VIDEO_ONLY", 1, MavParamType.Int32, Arg.Any<CancellationToken>());
        await model.DeactivateAsync();
        await parameters.Received(1).SetParameterAsync(id, "VIDEO_ONLY", 0, MavParamType.Int32, Arg.Any<CancellationToken>());
        Assert.False(model.CanFocus);
        await model.DeactivateAsync();
        await parameters.Received(1).SetParameterAsync(id, "VIDEO_ONLY", 0, MavParamType.Int32, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfflineOrUnsupportedDeviceCannotEnterFocusMode(bool online)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(new VehicleId(1, 1));
        active.IsOnline.Returns(online);
        var parameters = Substitute.For<IVehicleParameterService>();
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(x => x.Dispatch(Arg.Any<Action>())).Do(x => x.Arg<Action>()());
        var model = new PX4FlowViewModel(active, parameters, Substitute.For<IVehicleParameterRegistry>(),
            Substitute.For<IEventHub>(), NullLogger<PX4FlowViewModel>.Instance, dispatcher, Substitute.For<IDomainEventHub>());
        await model.ActivateAsync();
        Assert.False(model.CanFocus);
        await model.ToggleFocusCommand.ExecuteAsync(null);
        await parameters.DidNotReceiveWithAnyArgs().SetParameterAsync(default, default!, default, default, Arg.Any<CancellationToken>());
        await model.DeactivateAsync();
    }
}

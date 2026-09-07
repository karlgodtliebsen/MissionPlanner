using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Advanced;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class AdvancedHubTests
{
    [Fact]
    public void RegistryRejectsDuplicateUnknownAndMissingTargets()
    {
        var entry = new AdvancedToolRegistration(AdvancedFeatureId.Warnings, () => null!);
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([entry, entry]));
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([entry with { CreatePage = null! }]));
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([entry with { Id = (AdvancedFeatureId)99 }]));
        Assert.Throws<ArgumentException>(() => new AdvancedToolRegistry([]).Create("SetupAdvanced/Warnings"));
        Assert.Throws<InvalidOperationException>(() => new AdvancedToolRegistry([entry]).Create("SetupAdvanced/Warnings"));
    }

    [Fact]
    public async Task TenActivationCyclesDoNotAccumulateLaunchHandlersOrUpdateDetachedCards()
    {
        var platform = new AdvancedPlatformCapabilitySource();
        var vehicle = Substitute.For<IActiveVehicleContext>();
        var connection = Substitute.For<IVehicleConnectionService>();
        var navigation = Substitute.For<INavigationService>();
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(item => item.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
        var registry = new AdvancedToolRegistry([new(AdvancedFeatureId.Warnings, () => null!)]);
        using var model = new AdvancedViewModel(platform, vehicle, connection, new(), registry, navigation,
            Substitute.For<IVehicleParameterRegistry>(), NullLogger<AdvancedViewModel>.Instance, dispatcher, Substitute.For<IDomainEventHub>());
        var card = model.Tools[0];
        for (var iteration = 0; iteration < 10; iteration++)
        {
            await model.ActivateAsync();
            await model.ActivateAsync();
            card.LaunchCommand.Execute(null);
            await model.DeactivateAsync();
            card.LaunchCommand.Execute(null);
        }
        await navigation.Received(10).NavigateAsync("SetupAdvanced/Warnings");
        var before = model.Tools[4].Availability;
        platform.Update(new("Test", SerialOutput: true));
        Assert.Same(before, model.Tools[4].Availability);
        await model.ActivateAsync();
        Assert.Equal(AdvancedAvailabilityState.ConnectionRequired, model.Tools[4].Availability.State);
    }
}

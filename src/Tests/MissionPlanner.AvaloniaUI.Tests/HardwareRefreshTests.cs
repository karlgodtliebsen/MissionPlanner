using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies explicit hardware refresh and cancellation at the page boundary.</summary>
[Collection("Document rendering")]
public sealed class HardwareRefreshTests
{
    /// <summary>Provides the presentation services used by hardware ViewModels.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(x => x.Dispatch(Arg.Any<Action>())).Do(x => x.Arg<Action>()!());
        dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(x => x.Arg<Func<Task>>()!());
        var services = new ServiceCollection().AddLogging().AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IDomainEventHub>()).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    /// <summary>Refresh requests current values, reloads the module and ignores replies after deactivation.</summary>
    [Fact]
    public async Task RefreshReadsValuesAndDropsLateResults()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(HardwareRefreshTests));
        try
        {
            await session.Dispatch(async () =>
            {
                var active = Substitute.For<IActiveVehicleContext>();
                var id = new VehicleId(1, 1);
                active.VehicleId.Returns(id);
                active.IsOnline.Returns(true);
                var hardware = Substitute.For<IOptionalHardwareService>();
                var navigation = Substitute.For<INavigationService>();
                IReadOnlyList<OptionalHardwareModuleView> Modules(string message) =>
                    [new("airspeed", "Airspeed", message, [], [], null)];
                hardware.GetModulesAsync(id, Arg.Any<CancellationToken>()).Returns(Modules("Initial"));
                using var model = new AirspeedViewModel(active, hardware, NullLogger<AirspeedViewModel>.Instance, navigation);
                await model.ActivateAsync();
                Assert.Equal("Initial", model.StatusMessage);

                hardware.GetModulesAsync(id, Arg.Any<CancellationToken>()).Returns(Modules("Refreshed"));
                await model.RefreshCommand.ExecuteAsync(null);
                await hardware.Received(1).RefreshAsync(id, Arg.Any<CancellationToken>());
                Assert.Equal("Refreshed", model.StatusMessage);
                await model.OpenFullParametersCommand.ExecuteAsync(null);
                await navigation.Received(1).NavigateAsync(MissionPlannerRoutes.ConfigFullParameters);

                var late = new TaskCompletionSource<IReadOnlyList<OptionalHardwareModuleView>>();
                CancellationToken requestToken = default;
                hardware.GetModulesAsync(id, Arg.Any<CancellationToken>()).Returns(call =>
                {
                    requestToken = call.Arg<CancellationToken>();
                    return late.Task;
                });
                var refresh = model.RefreshCommand.ExecuteAsync(null);
                Assert.True(model.IsBusy);
                await model.DeactivateAsync();
                Assert.True(requestToken.IsCancellationRequested);
                late.SetResult(Modules("Stale result"));
                await refresh;
                Assert.NotEqual("Stale result", model.StatusMessage);
                Assert.False(model.IsBusy);

                active.IsOnline.Returns(false);
                await model.ActivateAsync();
                Assert.Empty(model.Settings);
                Assert.Contains("Connect a vehicle", model.StatusMessage);
                await model.DeactivateAsync();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

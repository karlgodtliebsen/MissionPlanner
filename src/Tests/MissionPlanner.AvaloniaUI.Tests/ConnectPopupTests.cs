using Avalonia;
using System.Net;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Services.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class ConnectPopupTests
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(x => x.Dispatch(Arg.Any<Action>())).Do(x => x.Arg<Action>()());
        dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(x => x.Arg<Func<Task>>()());
        var services = new ServiceCollection().AddLogging().AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IDomainEventHub>()).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task AutoPrefersComAndRequiresDetectedUdpBeforeConnecting()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ConnectPopupTests));
        try
        {
            await session.Dispatch(async () =>
            {
                var active = Substitute.For<IActiveVehicleContext>();
                active.Current.Returns(ActiveVehicleSnapshot.Empty);
                using var state = new ApplicationStateService(active);
                var options = Substitute.For<IOptionsMonitor<ApplicationOptions>>();
                options.CurrentValue.Returns(new ApplicationOptions());
                var catalog = Substitute.For<IFirmwareSerialDeviceCatalog>();
                IReadOnlyList<SerialDeviceDescriptor> devices = [new("COM4")];
                catalog.GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(_ => devices);
                var connection = Substitute.For<IVehicleConnectionService>();
                var udp = Substitute.For<IUdpVehicleDiscovery>();
                connection.ConnectSerialAsync("COM4", 115200, Arg.Any<CancellationToken>())
                    .Returns(new VehicleConnectionResult(false, null, null, "Test heartbeat absent"));
                using var model = new ConnectPopupViewModel(Substitute.For<ISerialPortDiscoveryService>(), connection,
                    Substitute.For<IDomainEventHub>(), state, options, NullLogger<ConnectPopupViewModel>.Instance, catalog, udp);
                await model.RefreshCommand.ExecuteAsync(null);
                Assert.Equal("COM4", model.SelectedChannel);
                Assert.Empty(udp.ReceivedCalls());

                model.SelectedChannel = "AUTO";
                await model.ConnectCommand.ExecuteAsync(null);
                await connection.Received(1).ConnectSerialAsync("COM4", 115200, Arg.Any<CancellationToken>());
                Assert.Empty(udp.ReceivedCalls());

                connection.ClearReceivedCalls();
                devices = [];
                model.SelectedChannel = "AUTO";
                await model.ConnectCommand.ExecuteAsync(null);
                Assert.Contains("no available serial device", model.StatusMessage);
                Assert.Equal("AUTO", model.SelectedChannel);
                Assert.Empty(connection.ReceivedCalls());
                Assert.False(model.IsConnecting);
                await model.RefreshCommand.ExecuteAsync(null);
                Assert.DoesNotContain("COM4", model.Channels);

                devices = [new("COM4"), new("COM5")];
                model.SelectedChannel = "AUTO";
                await model.ConnectCommand.ExecuteAsync(null);
                Assert.Contains("multiple serial devices", model.StatusMessage);
                Assert.Empty(connection.ReceivedCalls());

                devices = [new("COM4", usbIdentifier: new(0x0483, 0xdf11))];
                await model.ConnectCommand.ExecuteAsync(null);
                Assert.Contains("no available serial device", model.StatusMessage);
                Assert.Empty(connection.ReceivedCalls());

                devices = [];
                udp.FindAsync(14550, Arg.Any<CancellationToken>()).Returns(new IPEndPoint(IPAddress.Loopback, 15555));
                connection.ConnectUdpAsync(14550, "127.0.0.1", 15555, Arg.Any<CancellationToken>())
                    .Returns(new VehicleConnectionResult(false, null, null, "Test ended"));
                model.SelectedChannel = "AUTO";
                await model.ConnectCommand.ExecuteAsync(null);
                await connection.Received(1).ConnectUdpAsync(14550, "127.0.0.1", 15555, Arg.Any<CancellationToken>());
                Assert.Equal("UDP", model.SelectedChannel);
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

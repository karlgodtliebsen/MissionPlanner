using Avalonia;
using MissionPlanner.Core.ConfigTuning.Planner;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
    [Fact]
    public async Task EndpointDraftsPersistAndClosingDoesNotCancelAnEstablishedConnection()
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
                var settings = Substitute.For<IPlannerSettingsService>();
                var saved = new PlannerSettings();
                settings.Current.Returns(_ => saved);
                settings.SaveAsync(Arg.Any<PlannerSettings>(), Arg.Any<CancellationToken>()).Returns(call =>
                {
                    saved = call.Arg<PlannerSettings>();
                    return new PlannerSettingsSaveResult(true, [], []);
                });
                var connection = Substitute.For<IVehicleConnectionService>();
                CancellationToken connectionToken = default;
                connection.ConnectNetworkAsync(Arg.Any<NetworkConnectionSettings>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
                    .Returns(call =>
                    {
                        connectionToken = call.Arg<CancellationToken>();
                        return new VehicleConnectionResult(true, new(41, 1), null);
                    });
                var serial = Substitute.For<ISerialPortDiscoveryService>();
                using (var model = new ConnectPopupViewModel(serial, connection, Substitute.For<IDomainEventHub>(), state,
                    options, NullLogger<ConnectPopupViewModel>.Instance, plannerSettings: settings))
                {
                    model.SelectedChannel = "UDPCl";
                    model.SelectedHost = "test.local";
                    model.SelectedPort = "14563";
                    await model.ConnectCommand.ExecuteAsync(null);
                }
                Assert.False(connectionToken.IsCancellationRequested);
                Assert.Equal("14563", saved.Connection.NetworkDrafts["UDPCl"].Port);
                using var reopened = new ConnectPopupViewModel(serial, connection, Substitute.For<IDomainEventHub>(), state,
                    options, NullLogger<ConnectPopupViewModel>.Instance, plannerSettings: settings);
                reopened.SelectedChannel = "UDPCl";
                Assert.Equal("test.local", reopened.SelectedHost);
                Assert.Equal("14563", reopened.SelectedPort);
                reopened.SelectedChannel = "WS";
                reopened.WebSocketUrl = "ws://test.local/mavlink?token=secret";
                await reopened.ConnectCommand.ExecuteAsync(null);
                Assert.DoesNotContain("secret", saved.Connection.NetworkDrafts["WS"].Url);
                Assert.Equal("ws://test.local/mavlink?token=secret", reopened.WebSocketUrl);
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(x => x.Dispatch(Arg.Any<Action>())).Do(x => x.Arg<Action>()());
        dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(x => x.Arg<Func<Task>>()());
        var services = new ServiceCollection().AddLogging().AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IDomainEventHub>()).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
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

                connection.ClearReceivedCalls();
                model.SelectedChannel = "UDPCl";
                Assert.True(model.ShowRemoteEndpoint);
                Assert.False(model.ShowBaudRate);
                model.SelectedHost = "bridge.local";
                model.SelectedPort = "14561";
                model.ClientLocalPort = "14600";
                model.SelectedChannel = "WS";
                model.WebSocketUrl = "ws://localhost:8765/mavlink?stream=2";
                Assert.Equal("", model.NetworkValidationMessage);
                Assert.True(model.ShowWebSocket);
                model.SelectedChannel = "UDPCl";
                Assert.Equal("bridge.local", model.SelectedHost);
                Assert.Equal("14561", model.SelectedPort);
                Assert.Equal("14600", model.ClientLocalPort);
                model.SelectedPort = "65536";
                await model.ConnectCommand.ExecuteAsync(null);
                Assert.Contains("65535", model.StatusMessage);
                Assert.Empty(connection.ReceivedCalls());
                model.SelectedPort = "14561";
                await model.ConnectCommand.ExecuteAsync(null);
                await connection.Received(1).ConnectNetworkAsync(Arg.Is<NetworkConnectionSettings>(settings =>
                    settings.Channel == "UDPCl" && settings.RemoteHost == "bridge.local" && settings.RemotePort == 14561
                    && settings.LocalPort == 14600), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());
                connection.ClearReceivedCalls();
                model.SelectedChannel = "WS";
                Assert.Equal("ws://localhost:8765/mavlink?stream=2", model.WebSocketUrl);
                model.WebSocketUrl = "https://localhost/";
                Assert.NotEmpty(model.NetworkValidationMessage);
                model.WebSocketUrl = "wss://localhost:8765/mavlink";
                Assert.Empty(model.NetworkValidationMessage);
                await model.ConnectCommand.ExecuteAsync(null);
                await connection.Received(1).ConnectNetworkAsync(Arg.Is<NetworkConnectionSettings>(settings =>
                    settings.WebSocketUrl == "wss://localhost:8765/mavlink"), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());

                var view = new ConnectPopupView { DataContext = model };
                var window = new Window { Width = 380, Height = 650, Content = view };
                window.Show();
                foreach (var channel in new[] { "UDP", "UDPCl", "WS" })
                {
                    model.SelectedChannel = channel;
                    model.StatusMessage = "";
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var output = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        using var bitmap = new RenderTargetBitmap(new PixelSize(380, 650));
                        bitmap.Render(window);
                        bitmap.Save(Path.Combine(output, $"connection-{channel}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    }
                }
                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Test.Support.Configuration;

namespace MissionPlanner.Core.Tests;

/// <summary>Opt-in tests for isolated SITL instances only; no hardware endpoints are used by default.</summary>
public sealed class NetworkSitlTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("MP_NETWORK_SITL") == "1";

    [Theory(Skip = "Set MP_NETWORK_SITL=1 only for the isolated simulation setup.", SkipUnless = nameof(Enabled))]
    [InlineData(false, 41)]
    [InlineData(true, 42)]
    public async Task SitlTelemetryCompleteParameterDownloadCommandAndReconnect(bool websocket, int systemId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        await using var provider = TestConfigurator.AddTestConfiguration(null).BuildServiceProvider();
        provider.UseTestConfiguration();
        var connection = provider.GetRequiredService<IVehicleConnectionService>();
        var session = provider.GetRequiredService<IVehicleConnectionSession>();
        var vehicles = provider.GetRequiredService<IVehicleService>();
        var commands = provider.GetRequiredService<IVehicleCommandService>();
        var host = Environment.GetEnvironmentVariable("MP_NETWORK_SITL_HOST") ?? "127.0.0.1";
        var settings = websocket
            ? new NetworkConnectionSettings { Channel = "WS", WebSocketUrl = $"ws://{host}:18765/" }
            : new NetworkConnectionSettings { Channel = "UDPCl", RemoteHost = host, RemotePort = 14590, LocalPort = 0 };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await connection.ConnectNetworkAsync(settings, cancellationToken: token);
            Assert.True(result.Success, result.ErrorMessage);
            var id = result.VehicleId!.Value;
            Assert.Equal(systemId, id.SystemId);
            while (vehicles.GetVehicleState(id)?.Roll is null)
            {
                await Task.Delay(50, token);
            }
            Assert.False(vehicles.GetVehicleState(id)!.IsArmed);
            Assert.True(await session.ParameterService.RequestParameterListAsync(id, token));
            while (session.ParameterRegistry.GetParameterCount(id) is not > 100
                || session.ParameterRegistry.GetAllParameters(id).Count != session.ParameterRegistry.GetParameterCount(id))
            {
                await Task.Delay(100, token);
            }
            // Read-only REQUEST_MESSAGE(AUTOPILOT_VERSION), acknowledged by the simulated FC.
            var response = await commands.ExecuteExpertAsync(new(id, 512, [148, 0, 0, 0, 0, 0, 0]), true, token);
            Assert.True(response.Result == VehicleCommandResult.Accepted, response.Message);
            await connection.DisconnectAsync(token);
            Assert.False(connection.IsConnected);
        }
    }
}

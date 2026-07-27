using System.Net;
using System.Net.Sockets;

namespace MissionPlanner.Simulator;

/// <summary>
/// A simple MAVLink vehicle simulator that sends fake MAVLink messages over UDP. 
/// </summary>
public sealed class FakeMavLinkVehicle : IAsyncDisposable
{
    //HEARTBEAT
    //SYS_STATUS
    //ATTITUDE later
    //COMMAND_ACK later
    private readonly UdpClient udpClient;
    private readonly IPEndPoint targetEndpoint;

    private CancellationTokenSource? cancellationTokenSource;
    private Task? workerTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeMavLinkVehicle"/> class.
    /// </summary>
    /// <param name="targetIp"></param>
    /// <param name="targetPort"></param>
    public FakeMavLinkVehicle(string targetIp, int targetPort)
    {
        udpClient = new UdpClient();
        targetEndpoint = new IPEndPoint(IPAddress.Parse(targetIp), targetPort);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Task StartAsync()
    {
        cancellationTokenSource = new CancellationTokenSource();

        workerTask = Task.Run(() => SendLoopAsync(cancellationTokenSource.Token));

        return Task.CompletedTask;
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var payload = CreateFakeHeartbeat();

            await udpClient.SendAsync(payload, targetEndpoint, cancellationToken);

            //await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
        }
    }

    private static byte[] CreateFakeHeartbeat()
    {
        return
        [
            0xFD,
            0x09,
            0x00,
            0x00,
            0x00,
            0x01,
            0x01,
            0x00,
            0x00,
            0x00,
            0x01,
            0x02,
            0x03,
            0x04,
            0x05,
            0x06
        ];
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (cancellationTokenSource != null)
        {
            await cancellationTokenSource.CancelAsync();

            if (workerTask != null) await workerTask;

            cancellationTokenSource.Dispose();
        }

        udpClient.Dispose();
    }
}
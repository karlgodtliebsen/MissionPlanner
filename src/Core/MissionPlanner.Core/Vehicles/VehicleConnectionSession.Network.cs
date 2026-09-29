using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Vehicles;

public sealed partial class VehicleConnectionSession
{
    /// <inheritdoc />
    public async Task<CancellationTokenSource> CreateNetworkConnection(NetworkConnectionSettings settings,
        byte gcsSystemId = 255, CancellationToken cancellationToken = default)
    {
        if (settings.Validate() is { } error)
        {
            throw new ArgumentException(error);
        }
        serviceCts = new CancellationTokenSource();
        DisconnectReason = null;
        if (resetRegistryOnLifecycle)
        {
            await serviceFactory.Create<IVehicleRegistry>().Reset(cancellationToken).ConfigureAwait(false);
        }
        ActiveTransportProtocol = settings.Channel.ToLowerInvariant();
        ActiveSerialPort = null;
        var options = Options.Create(new TransportEndpoint
        {
            Protocol = ActiveTransportProtocol,
            IsUdpClient = settings.Channel == "UDPCl",
            RemoteHost = settings.RemoteHost,
            RemotePort = settings.RemotePort,
            LocalHost = settings.LocalBindAddress,
            LocalPort = settings.LocalPort,
            WebSocketUrl = settings.WebSocketUrl
        });
        messagePumpLease = await messagePumpCoordinator.AcquireAsync(cancellationToken).ConfigureAwait(false);
        messagePump = messagePumpLease.Pump;
        connectionSession = await connectionSessionFactory.CreateNetworkConnection(options, gcsSystemId, cancellationToken).ConfigureAwait(false);
        parameterService = domainFactory.Create<IVehicleParameterService, IVehicleConnectionSession>(this);
        parameterStreamService = CreateParameterStreamService();
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token,
            connectionSession.Connection.Activity?.LifetimeToken ?? CancellationToken.None);
    }
}

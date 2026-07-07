using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Core.Configuration;

/// <summary>
/// 
/// </summary>
public static class DomainConfigurator
{
    /// <summary>
    /// Adds domain services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to which domain services will be added.</param>
    /// <param name="configuration"></param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddTransient<IVehicleMessagePump, VehicleMessagePump>();
        services.TryAddTransient<IVehicleConnectionMonitor, VehicleConnectionMonitor>();

        services.TryAddTransient<IHeartbeatVehicleHandler, HeartbeatVehicleHandler>();
        services.TryAddTransient<IAttitudeVehicleHandler, AttitudeVehicleHandler>();
        services.TryAddTransient<IBatteryVehicleHandler, BatteryVehicleHandler>();
        services.TryAddTransient<IPositionVehicleHandler, PositionVehicleHandler>();
        services.TryAddTransient<IStatusTextHandler, StatusTextHandler>();

        services.TryAddTransient<ICommandAckTracker, CommandAckTracker>();
        services.TryAddTransient<IVehicleCommandPolicy, VehicleCommandPolicy>();

        services.TryAddTransient<ISerialMavLinkTransport, SerialMavLinkTransport>();
        services.TryAddTransient<IUdpMavLinkTransport, UdpMavLinkTransport>();
        services.TryAddTransient<ITcpMavLinkTransport, TcpMavLinkTransport>();

        services.TryAddSingleton<IVehicleRegistry, VehicleRegistry>();

        services.TryAddTransient<ISerialPortDiscoveryService, SerialPortDiscoveryService>();

        services.TryAddSingleton<IVehicleConnectionService, VehicleConnectionService>();
        services.TryAddTransient<IVehicleHudDataService, VehicleHudDataService>();


        services.TryAddTransient<IVehicleCommandService, VehicleCommandService>();
        services.TryAddTransient<IVehicleService, VehicleService>();

        // MAVLink command sending services
        services.TryAddTransient<IMavLinkCommandService, MavLinkCommandService>();

        return services;
    }

    /// <summary>
    /// Configures domain services using the specified <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="services">The service provider to which domain services will be added.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseDomainServices(this IServiceProvider services)
    {
        var domainFactory = services.GetRequiredService<IDomainFactory>();
        domainFactory.Add<IHeartbeatVehicleHandler, HeartbeatVehicleHandler>();
        domainFactory.Add<IAttitudeVehicleHandler, AttitudeVehicleHandler>();
        domainFactory.Add<IBatteryVehicleHandler, BatteryVehicleHandler>();
        domainFactory.Add<IPositionVehicleHandler, PositionVehicleHandler>();
        domainFactory.Add<IVehicleMessagePump, VehicleMessagePump>();
        domainFactory.Add<ISerialMavLinkTransport, SerialMavLinkTransport>();
        domainFactory.Add<IUdpMavLinkTransport, UdpMavLinkTransport>();
        domainFactory.Add<ITcpMavLinkTransport, TcpMavLinkTransport>();
        domainFactory.Add<IMavLinkClient, MavLinkClient>();
        domainFactory.Add<IMavLinkConnection, MavLinkConnection>();
        domainFactory.Add<IMavLinkCommandService, MavLinkCommandService>();
        return services;
    }
}

//"name": "DroneBridge 1",
//"host": "192.168.1.217",
//"port": 5760, //14550
//"protocol": "udp",  
//"expectedSystemId": "optional"

using Domain.Library.Factory.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.VehicleHandler;

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
        services.TryAddSingleton<IVehicleMessagePump, VehicleMessagePump>();
        services.TryAddSingleton<IVehicleConnectionMonitor, VehicleConnectionMonitor>();

        services.TryAddSingleton<IHeartbeatVehicleHandler, HeartbeatVehicleHandler>();
        services.TryAddSingleton<IAttitudeVehicleHandler, AttitudeVehicleHandler>();
        services.TryAddSingleton<IBatteryVehicleHandler, BatteryVehicleHandler>();
        services.TryAddSingleton<IPositionVehicleHandler, PositionVehicleHandler>();
        services.TryAddSingleton<IStatusTextHandler, StatusTextHandler>();

        services.TryAddSingleton<IDomainEventHub, DomainEventHub>();
        services.TryAddSingleton<ICommandAckTracker, CommandAckTracker>();
        services.TryAddSingleton<IVehicleCommandPolicy, VehicleCommandPolicy>();


        services.TryAddSingleton<IVehicleRegistry, VehicleRegistry>();

        services.TryAddSingleton<ISerialPortDiscoveryService, SerialPortDiscoveryService>();
        services.TryAddSingleton<IVehicleConnectionService, VehicleConnectionService>();
        services.TryAddSingleton<IVehicleHudDataService, VehicleHudDataService>();

        services.TryAddTransient<IVehicleCommandService, VehicleCommandService>();
        services.TryAddTransient<IVehicleService, VehicleService>();

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
        return services;
    }
}

//"name": "DroneBridge 1",
//"host": "192.168.1.217",
//"port": 5760, //14550
//"protocol": "udp",  
//"expectedSystemId": "optional"

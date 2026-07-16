using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Missions.Validation;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
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
        services.TryAddTransient<IMissionTransferService, MissionTransferService>();
        services.TryAddTransient<IMissionProtocolMapper, MissionProtocolMapper>();
        services.TryAddTransient<IMissionValidator, MissionValidator>();
        services.TryAddTransient<IMissionFileCodec, MissionFileCodec>();


        services.TryAddTransient<IVehicleMessagePump, VehicleMessagePump>();
        services.TryAddTransient<IVehicleConnectionMonitor, VehicleConnectionMonitor>();


        services.TryAddTransient<ICommandAckTracker, CommandAckTracker>();
        services.TryAddTransient<IVehicleCommandPolicy, VehicleCommandPolicy>();

        services.TryAddTransient<ISerialMavLinkTransport, SerialMavLinkTransport>();
        services.TryAddTransient<IUdpMavLinkTransport, UdpMavLinkTransport>();
        services.TryAddTransient<ITcpMavLinkTransport, TcpMavLinkTransport>();
        services.TryAddSingleton<IVehicleConnectionSession, VehicleConnectionSession>();

        services.TryAddSingleton<IVehicleRegistry, VehicleRegistry>();
        services.TryAddSingleton<IVehicleParameterRegistry, VehicleParameterRegistry>();

        services.TryAddTransient<ISerialPortDiscoveryService, SerialPortDiscoveryService>();

        services.TryAddSingleton<IVehicleConnectionService, VehicleConnectionService>();
        services.TryAddSingleton<IVehicleHudDataService, VehicleHudDataService>();

        services.TryAddTransient<IStatusTextHandler, StatusTextHandler>();
        services.TryAddTransient<IParamValueVehicleHandler, ParamValueVehicleHandler>();

        services.TryAddTransient<IVehicleMessageDispatcher, VehicleMessageDispatcher>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, FlightTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, NavigationTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, PowerTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, RadioTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, HealthTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, ControlMessageHandler>());


        services.TryAddTransient<IVehicleCommandService, VehicleCommandService>();
        services.TryAddTransient<IVehicleService, VehicleService>();

        // MAVLink command sending services
        services.TryAddTransient<IMavLinkCommandService, MavLinkCommandService>();

        // MAVLink parameter services
        services.TryAddTransient<IVehicleParameterService, VehicleParameterService>();
        services.TryAddSingleton<IVehicleParameterMetadataService, VehicleParameterMetadataService>();
        services.TryAddTransient<IVehicleParameterStreamService, VehicleParameterStreamService>();

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
        domainFactory.Add<IParamValueVehicleHandler, ParamValueVehicleHandler>();


        domainFactory.Add<IVehicleMessagePump, VehicleMessagePump>();
        domainFactory.Add<ISerialMavLinkTransport, SerialMavLinkTransport>();
        domainFactory.Add<IUdpMavLinkTransport, UdpMavLinkTransport>();
        domainFactory.Add<ITcpMavLinkTransport, TcpMavLinkTransport>();
        domainFactory.Add<IMavLinkClient, MavLinkClient>();
        domainFactory.Add<IMavLinkConnection, MavLinkConnection>();
        domainFactory.Add<IMavLinkCommandService, MavLinkCommandService>();
        domainFactory.Add<IVehicleParameterService, VehicleParameterService>();
        domainFactory.Add<IVehicleParameterStreamService, VehicleParameterStreamService>();
        return services;
    }
}

using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.Core.Simulation;

/// <summary>Creates independently owned MAVLink sessions for direct SITL runtimes.</summary>
public sealed class SimulatorVehicleConnectionFactory(
    IVehicleParameterRegistry parameterRegistry,
    IDomainFactory domainFactory,
    IServiceFactory serviceFactory,
    IDomainEventHub domainEventHub,
    IDateTimeProvider clock,
    IVehicleMessagePumpCoordinator messagePumpCoordinator,
    IVehicleRegistry vehicleRegistry,
    ISimulationVehicleChannelRegistry channelRegistry,
    ILoggerFactory loggerFactory) : ISimulatorVehicleConnectionFactory
{
    /// <inheritdoc />
    public ISimulatorVehicleConnection Create(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A simulator connection requires a non-empty session identity.", nameof(sessionId));
        }

        var session = new VehicleConnectionSession(
            parameterRegistry,
            domainFactory,
            serviceFactory,
            domainEventHub,
            clock,
            loggerFactory.CreateLogger<VehicleConnectionSession>(),
            messagePumpCoordinator,
            false);
        return new IsolatedSimulatorVehicleConnection(
            sessionId,
            session,
            vehicleRegistry,
            domainEventHub,
            clock,
            domainFactory,
            channelRegistry,
            loggerFactory.CreateLogger<IsolatedSimulatorVehicleConnection>());
    }
}

using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>Creates the production direct SLCAN or captured MAVLink CAN adapter.</summary>
public sealed class DroneCanTransportFactory(IDirectSerialSessionFactory serial, IActiveVehicleContext active, IVehicleRegistry registry,
    IEventHub events, IMavLinkConnection connection, IMavLinkWireMessageEncoder wire, IMavLinkCommandEncoder commands,
    IVehicleOperationGate operations, IDroneCanAllocationStore allocations) : IDroneCanTransportFactory
{
    /// <inheritdoc />
    public IDroneCanTransport Create(DroneCanTransportKind kind) => Create(kind, new());
    /// <inheritdoc />
    public IDroneCanTransport Create(DroneCanTransportKind kind, DroneCanConnectionOptions options)
    {
        options.Validate(kind);
        IDroneCanFrameLink link = kind switch
        {
            DroneCanTransportKind.DirectSlcan => new SlcanFrameLink(serial, options),
            DroneCanTransportKind.MavLinkTunnel => new MavLinkCanFrameLink(active, registry, events, connection, wire, commands, operations, options),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return new DroneCanTransport(link, kind, options, allocations);
    }
}

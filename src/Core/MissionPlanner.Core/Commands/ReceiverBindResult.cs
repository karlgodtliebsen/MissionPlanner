using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>Evidence used to expose FC-initiated receiver binding.</summary>
/// <param name="IsAvailable">Whether current safety and configuration checks allow binding.</param>
/// <param name="ReceiverProtocol">Known receiver protocol, or Unknown.</param>
/// <param name="Capability">Expected when configuration selects CRSF exclusively; otherwise Unknown.</param>
/// <param name="Reason">Availability explanation.</param>
public sealed record ReceiverBindAvailability(bool IsAvailable, string ReceiverProtocol, string Capability, string Reason);

/// <summary>Command outcome, not proof that a physical receiver entered bind mode.</summary>
/// <param name="Response">Acknowledged command outcome.</param>
/// <param name="CorrelationId">Identifier shared with the diagnostic transaction.</param>
public sealed record ReceiverBindResult(VehicleCommandResponse Response, Guid CorrelationId);

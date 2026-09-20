using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

/// <summary>Evidence-based arming explanation; accepted commands never replace heartbeat truth.</summary>
/// <param name="Summary">UI-ready readiness or connection summary.</param>
/// <param name="IsArmed">Authoritative heartbeat armed state.</param>
/// <param name="IsReadyToArm">Confirmed readiness, or unknown.</param>
/// <param name="Reasons">Current concrete blockers.</param>
/// <param name="LastArmFailure">Retained attempt failure history.</param>
/// <param name="LastArmAttemptAt">Latest requested arm transaction time.</param>
/// <param name="LastArmResult">Latest acknowledgement or transaction outcome.</param>
public sealed record VehicleArmingDiagnostic(string Summary, bool IsArmed, bool? IsReadyToArm,
    IReadOnlyList<string> Reasons, string? LastArmFailure, DateTimeOffset? LastArmAttemptAt, string? LastArmResult);

/// <summary>Normalized command transaction evidence from domain command owners.</summary>
/// <param name="VehicleId">Target vehicle.</param>
/// <param name="At">Evidence time.</param>
/// <param name="CommandId">MAVLink command identifier.</param>
/// <param name="CorrelationId">Request/response transaction identity.</param>
/// <param name="Stage">TX, ACK, or terminal outcome.</param>
/// <param name="Detail">Human-readable command parameters or result.</param>
/// <param name="IsArmRequest">True only for a request to arm, rather than disarm.</param>
public sealed record VehicleCommandDiagnostic(VehicleId VehicleId, DateTimeOffset At, ushort CommandId,
    Guid CorrelationId, string Stage, string Detail, bool IsArmRequest = false);

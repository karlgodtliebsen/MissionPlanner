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
    IReadOnlyList<string> Reasons, string? LastArmFailure, DateTimeOffset? LastArmAttemptAt, string? LastArmResult)
{
    /// <summary>Current evidence-based request/readiness stage.</summary>
    public ArmingDiagnosticStage Stage { get; init; }
    /// <summary>Latest request source; RC and stick sources are explicitly inferred.</summary>
    public string? LastArmCommandSource { get; init; }
    /// <summary>Latest matching MAVLink acknowledgement result.</summary>
    public byte? LastArmAck { get; init; }
    /// <summary>Historical pre-arm text, separate from current blockers.</summary>
    public string? LastPreArmReason { get; init; }
    /// <summary>Timestamp of historical pre-arm text.</summary>
    public DateTimeOffset? LastPreArmReasonAt { get; init; }
    /// <summary>Configuration advice when no request was observed.</summary>
    public string? Guidance { get; init; }
}

/// <summary>Arming evidence stage; only heartbeat telemetry establishes Armed.</summary>
public enum ArmingDiagnosticStage
{
    /// <summary>Insufficient fresh evidence.</summary>
    Unknown,
    /// <summary>Disarmed with healthy pre-arm checks.</summary>
    DisarmedReady,
    /// <summary>Disarmed with current pre-arm blockers.</summary>
    DisarmedNotReady,
    /// <summary>Arm request observed; waiting for armed heartbeat.</summary>
    ArmRequested,
    /// <summary>Arm request explicitly rejected.</summary>
    ArmRejected,
    /// <summary>Heartbeat reports armed.</summary>
    Armed,
    /// <summary>Disarm request observed; waiting for disarmed heartbeat.</summary>
    DisarmRequested
}

/// <summary>Normalized command transaction evidence from domain command owners.</summary>
/// <param name="VehicleId">Target vehicle.</param>
/// <param name="At">Evidence time.</param>
/// <param name="CommandId">MAVLink command identifier.</param>
/// <param name="CorrelationId">Request/response transaction identity.</param>
/// <param name="Stage">TX, ACK, or terminal outcome.</param>
/// <param name="Detail">Human-readable command parameters or result.</param>
/// <param name="IsArmRequest">True only for a request to arm, rather than disarm.</param>
public sealed record VehicleCommandDiagnostic(VehicleId VehicleId, DateTimeOffset At, ushort CommandId,
    Guid CorrelationId, string Stage, string Detail, bool IsArmRequest = false)
{
    /// <summary>Receiver protocol evidence for a binding request.</summary>
    public string? ReceiverProtocol { get; init; }

    /// <summary>Original MAVLink result byte, when an ACK was received.</summary>
    public byte? CommandAck { get; init; }

    /// <summary>Command-specific reason supplied by the vehicle.</summary>
    public int? VehicleReason { get; init; }
}

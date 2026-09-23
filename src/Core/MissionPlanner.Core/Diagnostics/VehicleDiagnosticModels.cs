using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

/// <summary>Bounded significant diagnostic evidence, independent of application log levels.</summary>
/// <param name="VehicleId">Owning vehicle.</param>
/// <param name="At">Evidence timestamp.</param>
/// <param name="Category">Diagnostic category.</param>
/// <param name="Message">Human-readable evidence.</param>
/// <param name="CorrelationId">Optional command transaction identity.</param>
public sealed record VehicleDiagnosticEvent(VehicleId VehicleId, DateTimeOffset At, string Category,
    string Message, Guid? CorrelationId = null);

/// <summary>Current domain state plus retained diagnostic context.</summary>
/// <param name="VehicleId">Owning vehicle.</param>
/// <param name="State">Authoritative immutable vehicle state, or absent before first observation.</param>
/// <param name="Transport">Reported connection transport.</param>
/// <param name="Endpoint">Reported connection endpoint.</param>
/// <param name="Disconnected">Whether the owning connection has ended.</param>
/// <param name="Version">Monotonic diagnostic revision.</param>
/// <param name="UpdatedAt">Last diagnostic update.</param>
public sealed record VehicleLiveDiagnosticSnapshot(VehicleId VehicleId, VehicleState? State,
    string? Transport, string? Endpoint, bool Disconnected, long Version, DateTimeOffset UpdatedAt);

/// <summary>Configures per-vehicle retention and transient diagnostic evidence.</summary>
public sealed class VehicleLiveDiagnosticOptions
{
    /// <summary>Maximum significant events retained per vehicle.</summary>
    public int JournalCapacity { get; set; } = 1000;

    /// <summary>Maximum advanced raw samples retained per vehicle.</summary>
    public int RawCapacity { get; set; } = 500;

    /// <summary>Time for unconfirmed pre-arm text to remain a current reason.</summary>
    public TimeSpan ReasonLifetime { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum age of a fresh servo output sample, matching the servo setup default.</summary>
    public TimeSpan OutputSampleLifetime { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>Always-on, platform-neutral diagnostic state and significant-event history.</summary>
public interface IVehicleLiveDiagnostics
{
    /// <summary>Gets known vehicle identities, including retained disconnected vehicles.</summary>
    IReadOnlyList<VehicleId> Vehicles { get; }

    /// <summary>Reads current immutable state and diagnostic context.</summary>
    VehicleLiveDiagnosticSnapshot GetSnapshot(VehicleId vehicleId);

    /// <summary>Exports current state, parameters and all retained evidence as indented JSON, independent of UI filters or freeze.</summary>
    string CreateSnapshotJson(VehicleId vehicleId);

    /// <summary>Reads recent significant events, newest first.</summary>
    IReadOnlyList<VehicleDiagnosticEvent> GetRecentEvents(VehicleId vehicleId, int maxCount = 1000);

    /// <summary>Gets bounded advanced wire samples, newest first, optionally filtered.</summary>
    IReadOnlyList<VehicleRawDiagnostic> GetRaw(VehicleId vehicleId, string? nameOrId = null, byte? system = null, byte? component = null);

    /// <summary>Gets output configuration and observed FC outputs, with no physical-motion inference.</summary>
    IReadOnlyList<string> GetOutputs(VehicleId vehicleId);

    /// <summary>Gets calibrated RC input evidence for one vehicle.</summary>
    IReadOnlyList<VehicleDiagnosticChannel> GetRcChannels(VehicleId vehicleId);

    /// <summary>Explains current arming readiness using heartbeat and concrete, expiring evidence.</summary>
    VehicleArmingDiagnostic GetArming(VehicleId vehicleId);

    /// <summary>Clears only diagnostic history, preserving state and log files.</summary>
    void ClearEvents(VehicleId vehicleId);

    /// <summary>Adds a user marker without altering telemetry log format.</summary>
    void AddMarker(VehicleId vehicleId, string? text);
}

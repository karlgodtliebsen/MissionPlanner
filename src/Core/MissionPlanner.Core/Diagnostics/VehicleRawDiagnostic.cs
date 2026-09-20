using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

/// <summary>Bounded advanced wire observation normalized outside the UI.</summary>
/// <param name="VehicleId">Owning connection vehicle.</param>
/// <param name="At">Wire timestamp.</param>
/// <param name="SystemId">Source system.</param>
/// <param name="ComponentId">Source component.</param>
/// <param name="MessageId">Wire message ID.</param>
/// <param name="Name">Known dialect name or Unknown.</param>
/// <param name="Summary">Direction and verification evidence.</param>
/// <param name="Payload">Exact payload bytes as hexadecimal.</param>
public sealed record VehicleRawDiagnostic(VehicleId VehicleId, DateTimeOffset At, byte SystemId,
    byte ComponentId, uint MessageId, string Name, string Summary, string Payload);

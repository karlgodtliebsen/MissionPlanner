using System.Text.Json;
using System.Text.Json.Serialization;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

public sealed partial class VehicleLiveDiagnostics
{
    /// <inheritdoc />
    public string CreateSnapshotJson(VehicleId vehicleId)
    {
        object snapshot;
        lock (sync)
        {
            var entry = Get(vehicleId);
            var capturedAt = clock.GetUtcNow();
            var vehicle = GetSnapshot(vehicleId);
            snapshot = new
            {
                SchemaVersion = 1,
                CapturedAt = capturedAt,
                SuggestedFilename = $"diagnostics-sys{vehicleId.SystemId}-comp{vehicleId.ComponentId}-{capturedAt:yyyyMMdd-HHmmssfff}.json",
                DataStatus = vehicle.Disconnected || vehicle.State?.Connection.State == VehicleConnectionState.Offline ? "Disconnected retained data" :
                    vehicle.State is null || capturedAt - vehicle.State.LastHeartbeatAt > TimeSpan.FromSeconds(3) ? "Stale or unknown" : "Recent heartbeat",
                Scope = "Current collected diagnostics; independent of Inspector freeze and filters.",
                Retention = new
                {
                    options.JournalCapacity,
                    options.RawCapacity,
                    Note = "Includes all currently retained evidence, not the complete recording."
                },
                Vehicle = vehicle,
                Arming = GetArming(vehicleId),
                RcChannels = GetRcChannels(vehicleId),
                Outputs = GetOutputs(vehicleId),
                Parameters = parameters?.GetAllParameters(vehicleId).ToDictionary(pair => pair.Key, pair => pair.Value),
                Events = entry.Journal.Reverse().OrderByDescending(item => item.At).ToArray(),
                Raw = entry.Raw.Reverse().OrderByDescending(item => item.At).ToArray()
            };
        }

        // Formatting happens only on request and outside the ingestion lock.
        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new JsonStringEnumConverter() }
        });
    }
}

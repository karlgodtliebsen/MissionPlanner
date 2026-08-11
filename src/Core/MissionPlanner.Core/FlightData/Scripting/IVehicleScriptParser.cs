using System.Text.Json;

namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Parses the safe JSON script format.</summary>
public interface IVehicleScriptParser
{
    /// <summary>Parses a document or throws <see cref="JsonException"/> for invalid JSON.</summary>
    VehicleScriptDocument Parse(string json);
}

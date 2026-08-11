using System.Text.Json;

namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>System.Text.Json parser for version 1 scripts.</summary>
public sealed class VehicleScriptParser : IVehicleScriptParser
{
    private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public VehicleScriptDocument Parse(string json)
    {
        return JsonSerializer.Deserialize<VehicleScriptDocument>(json, options)
               ?? throw new JsonException("The script document is empty.");
    }
}

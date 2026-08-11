namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Reviewed action registry with no arbitrary command or code escape hatch.</summary>
public sealed class VehicleScriptActionRegistry : IVehicleScriptActionRegistry
{
    /// <inheritdoc />
    public IReadOnlySet<string> Actions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "notify",
        "delay",
        "waitForConnection",
        "arm",
        "disarm",
        "land",
        "rtl",
        "hold",
        "auxFunction"
    };
}

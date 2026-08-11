namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Lists the only actions scripts may invoke.</summary>
public interface IVehicleScriptActionRegistry
{
    /// <summary>Gets the stable allow-listed action names.</summary>
    IReadOnlySet<string> Actions { get; }
}

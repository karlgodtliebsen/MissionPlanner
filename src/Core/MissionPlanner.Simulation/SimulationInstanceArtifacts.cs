namespace MissionPlanner.Simulation;

/// <summary>Contains isolated artifact locations for one deterministic SITL instance.</summary>
/// <param name="InstanceRootDirectory">Root directory uniquely assigned to the instance and SystemId.</param>
/// <param name="RuntimeLogDirectory">Working directory for runtime output and DataFlash files.</param>
/// <param name="TelemetryLogDirectory">Directory reserved for telemetry logs.</param>
/// <param name="DataFlashLogDirectory">Directory reserved for imported DataFlash logs.</param>
/// <param name="CacheDirectory">Directory reserved for instance-local caches.</param>
public sealed record SimulationInstanceArtifacts(
    string InstanceRootDirectory,
    string RuntimeLogDirectory,
    string TelemetryLogDirectory,
    string DataFlashLogDirectory,
    string CacheDirectory)
{
    /// <summary>Creates deterministic, category-isolated paths for one instance.</summary>
    /// <param name="rootDirectory">Configured simulation artifact root.</param>
    /// <param name="instance">SITL instance number.</param>
    /// <param name="systemId">MAVLink SystemId.</param>
    /// <returns>The isolated path set.</returns>
    public static SimulationInstanceArtifacts Create(string rootDirectory, int instance, byte systemId)
    {
        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Path.GetTempPath(), "MissionPlanner", "Simulation")
            : rootDirectory);
        var instanceRoot = Path.Combine(root, $"instance-{instance:D3}-sysid-{systemId:D3}");
        return new SimulationInstanceArtifacts(
            instanceRoot,
            Path.Combine(instanceRoot, "runtime"),
            Path.Combine(instanceRoot, "telemetry"),
            Path.Combine(instanceRoot, "dataflash"),
            Path.Combine(instanceRoot, "cache"));
    }

    /// <summary>Creates all isolated artifact directories.</summary>
    public void CreateDirectories()
    {
        Directory.CreateDirectory(RuntimeLogDirectory);
        Directory.CreateDirectory(TelemetryLogDirectory);
        Directory.CreateDirectory(DataFlashLogDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }
}

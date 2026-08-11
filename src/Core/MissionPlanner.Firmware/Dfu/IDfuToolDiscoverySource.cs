namespace MissionPlanner.Firmware.Dfu;

/// <summary>Discovers ordered STM32CubeProgrammer candidates without executing them.</summary>
public interface IDfuToolDiscoverySource
{
    /// <summary>Returns candidates in configured, known, registry, then PATH order.</summary>
    IReadOnlyList<DfuToolCandidate> Discover();
}

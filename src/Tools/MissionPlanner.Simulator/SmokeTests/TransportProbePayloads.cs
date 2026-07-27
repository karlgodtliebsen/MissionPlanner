namespace MissionPlanner.Simulator.SmokeTests;

/// <summary>
/// Provides payloads for transport probes used in smoke tests.
/// </summary>
public static class TransportProbePayloads
{
    /// <summary>
    /// Creates an ASCII probe payload for transport tests.
    /// </summary>
    /// <returns>A byte array representing the ASCII probe payload.</returns>
    public static byte[] CreateAsciiProbe()
    {
        return "DRONEGCS-PROBE"u8.ToArray();
    }
}
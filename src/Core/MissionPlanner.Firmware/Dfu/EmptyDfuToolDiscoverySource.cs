namespace MissionPlanner.Firmware.Dfu;

internal sealed class EmptyDfuToolDiscoverySource : IDfuToolDiscoverySource
{
    public IReadOnlyList<DfuToolCandidate> Discover()
    {
        return [];
    }
}

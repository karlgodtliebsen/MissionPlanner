using MissionPlanner.Core.ConfigTuning.VendorDevices;

namespace MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;

/// <summary>Contains verified configuration for one CubeLAN port.</summary>
/// <param name="PortIndex">The zero-based hardware port index.</param>
/// <param name="ClassOfServiceEnabled">Whether class-of-service processing is enabled.</param>
/// <param name="ClassOfServiceHighPriority">Whether the class-of-service high-priority bit is enabled.</param>
/// <param name="EnergyEfficientEthernetEnabled">Whether Energy Efficient Ethernet is enabled.</param>
/// <param name="VlanTagged">Whether VLAN egress is tagged.</param>
public sealed record CubeLanPortConfiguration(
    byte PortIndex,
    bool ClassOfServiceEnabled,
    bool ClassOfServiceHighPriority,
    bool EnergyEfficientEthernetEnabled,
    bool VlanTagged)
{
    /// <summary>Gets the protocol-faithful display label.</summary>
    public string DisplayName => $"Port {PortIndex}";
}

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

/// <summary>Contains one verified VLAN membership bit.</summary>
/// <param name="SourcePort">The source zero-based hardware port.</param>
/// <param name="DestinationPort">The destination zero-based hardware port.</param>
/// <param name="IsMember">Whether traffic from the source may reach the destination.</param>
public sealed record CubeLanVlanMembership(byte SourcePort, byte DestinationPort, bool IsMember);

/// <summary>Preserves one CubeLAN configuration register.</summary>
/// <param name="PhyAddress">The documented PHY page/address.</param>
/// <param name="Register">The register number.</param>
/// <param name="Value">The big-endian register value stored by CubeLAN.</param>
public sealed record CubeLanRegisterValue(byte PhyAddress, byte Register, ushort Value);

/// <summary>Contains the verified CubeLAN configuration subset and preserved registers.</summary>
/// <param name="Ports">The eight port settings.</param>
/// <param name="VlanMembership">The 8-by-8 VLAN membership matrix.</param>
/// <param name="Registers">All decoded registers, including unknown values preserved for round-trip safety.</param>
public sealed record CubeLanConfiguration(
    IReadOnlyList<CubeLanPortConfiguration> Ports,
    IReadOnlyList<CubeLanVlanMembership> VlanMembership,
    IReadOnlyList<CubeLanRegisterValue> Registers) : IVendorDeviceConfiguration
{
    /// <inheritdoc />
    public string Schema => "cubelan-switch-config-v1";
}

/// <summary>Describes a CubeLAN decode attempt.</summary>
/// <param name="Success">Whether a documented configuration envelope was decoded.</param>
/// <param name="Configuration">The decoded configuration.</param>
/// <param name="Message">The decode failure reason.</param>
public sealed record CubeLanDecodeResult(bool Success, CubeLanConfiguration? Configuration, string? Message);

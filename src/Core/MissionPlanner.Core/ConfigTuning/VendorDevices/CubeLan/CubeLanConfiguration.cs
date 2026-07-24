namespace MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;

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

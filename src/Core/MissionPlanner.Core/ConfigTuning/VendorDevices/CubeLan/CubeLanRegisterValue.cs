namespace MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;

/// <summary>Preserves one CubeLAN configuration register.</summary>
/// <param name="PhyAddress">The documented PHY page/address.</param>
/// <param name="Register">The register number.</param>
/// <param name="Value">The big-endian register value stored by CubeLAN.</param>
public sealed record CubeLanRegisterValue(byte PhyAddress, byte Register, ushort Value);

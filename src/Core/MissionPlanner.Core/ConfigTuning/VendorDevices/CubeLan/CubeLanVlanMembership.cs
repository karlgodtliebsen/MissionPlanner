namespace MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;

/// <summary>Contains one verified VLAN membership bit.</summary>
/// <param name="SourcePort">The source zero-based hardware port.</param>
/// <param name="DestinationPort">The destination zero-based hardware port.</param>
/// <param name="IsMember">Whether traffic from the source may reach the destination.</param>
public sealed record CubeLanVlanMembership(byte SourcePort, byte DestinationPort, bool IsMember);

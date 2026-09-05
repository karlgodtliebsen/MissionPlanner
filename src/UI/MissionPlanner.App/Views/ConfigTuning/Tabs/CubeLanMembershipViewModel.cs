using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Edits one CubeLAN VLAN membership destination.</summary>
public sealed partial class CubeLanMembershipViewModel : ObservableObject
{
    /// <summary>Initializes a membership editor.</summary>
    /// <param name="configuration">The confirmed membership value.</param>
    public CubeLanMembershipViewModel(CubeLanVlanMembership configuration)
    {
        SourcePort = configuration.SourcePort;
        DestinationPort = configuration.DestinationPort;
        IsMember = configuration.IsMember;
    }

    /// <summary>Gets the source hardware port.</summary>
    public byte SourcePort { get; }

    /// <summary>Gets the destination hardware port.</summary>
    public byte DestinationPort { get; }

    /// <summary>Gets the destination label.</summary>
    public string Label => $"To port {DestinationPort}";

    /// <summary>Gets whether this membership is enabled.</summary>
    [ObservableProperty]
    public partial bool IsMember { get; set; }
}


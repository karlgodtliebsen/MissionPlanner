using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>Edits the verified settings for one CubeLAN hardware port.</summary>
public sealed partial class CubeLanPortViewModel : ObservableObject
{
    /// <summary>Initializes a port editor.</summary>
    /// <param name="configuration">The confirmed port configuration.</param>
    /// <param name="memberships">The port's VLAN destination memberships.</param>
    public CubeLanPortViewModel(
        CubeLanPortConfiguration configuration,
        IEnumerable<CubeLanVlanMembership> memberships)
    {
        PortIndex = configuration.PortIndex;
        ClassOfServiceEnabled = configuration.ClassOfServiceEnabled;
        ClassOfServiceHighPriority = configuration.ClassOfServiceHighPriority;
        EnergyEfficientEthernetEnabled = configuration.EnergyEfficientEthernetEnabled;
        VlanTagged = configuration.VlanTagged;
        Memberships = new ObservableCollection<CubeLanMembershipViewModel>(
            memberships.OrderBy(item => item.DestinationPort).Select(item => new CubeLanMembershipViewModel(item)));
    }

    /// <summary>Gets the zero-based hardware port index.</summary>
    public byte PortIndex { get; }

    /// <summary>Gets the protocol-faithful port label.</summary>
    public string DisplayName => $"Port {PortIndex}";

    /// <summary>Gets whether class-of-service processing is enabled.</summary>
    [ObservableProperty]
    public partial bool ClassOfServiceEnabled { get; set; }

    /// <summary>Gets whether class-of-service high priority is enabled.</summary>
    [ObservableProperty]
    public partial bool ClassOfServiceHighPriority { get; set; }

    /// <summary>Gets whether Energy Efficient Ethernet is enabled.</summary>
    [ObservableProperty]
    public partial bool EnergyEfficientEthernetEnabled { get; set; }

    /// <summary>Gets whether VLAN egress is tagged.</summary>
    [ObservableProperty]
    public partial bool VlanTagged { get; set; }

    /// <summary>Gets the eight VLAN destination memberships.</summary>
    public ObservableCollection<CubeLanMembershipViewModel> Memberships { get; }
}


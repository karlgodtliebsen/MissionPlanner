using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Models;

/// <summary>Represents one fixed, availability-aware Optional Hardware tab header.</summary>
public sealed partial class TabItemViewModel : ObservableObject
{
    /// <summary>Initializes a tab item from its stable catalog descriptor.</summary>
    public TabItemViewModel(OptionalHardwareTabDescriptor descriptor) => Descriptor = descriptor;

    /// <summary>Gets the stable tab descriptor.</summary>
    public OptionalHardwareTabDescriptor Descriptor { get; }

    /// <summary>Gets the tab title.</summary>
    public string Title => Descriptor.Title;

    /// <summary>Gets the tab description.</summary>
    public string Description => Descriptor.Description;

    /// <summary>Gets whether the tab is currently available.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Gets the current availability status.</summary>
    [ObservableProperty]
    public partial string StateDisplay { get; private set; } = string.Empty;

    /// <summary>Applies a catalog availability evaluation.</summary>
    public void Update(OptionalHardwareTabState state)
    {
        IsAvailable = state.IsAvailable;
        StateDisplay = state.IsAvailable ? "Available" : state.ReasonUnavailable;
    }
}

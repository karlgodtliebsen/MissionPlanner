using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup;
namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;
/// <summary>Header state for one fixed Optional Hardware tab.</summary>
public sealed partial class OptionalHardwareTabItemViewModel(OptionalHardwareTabDescriptor descriptor) : ObservableObject
{
    /// <summary>Gets the descriptor.</summary>
    public OptionalHardwareTabDescriptor Descriptor { get; } = descriptor;
    /// <summary>Gets the title.</summary>
    public string Title => Descriptor.Title;
    /// <summary>Gets whether available.</summary>
    [ObservableProperty] public partial bool IsAvailable { get; private set; }
    /// <summary>Gets availability text.</summary>
    [ObservableProperty] public partial string StateDisplay { get; private set; } = string.Empty;
    /// <summary>Updates availability.</summary>
    public void Update(OptionalHardwareTabState state) { IsAvailable=state.IsAvailable; StateDisplay=state.IsAvailable?"Available":state.ReasonUnavailable; }
}

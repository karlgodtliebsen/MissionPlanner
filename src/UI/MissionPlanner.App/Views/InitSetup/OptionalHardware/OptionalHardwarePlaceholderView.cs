using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>Lifecycle-aware placeholder for a future specialty tab.</summary>
public sealed class OptionalHardwarePlaceholderView : TabViewLifecycleContent<OptionalHardwarePlaceholderViewModel>
{
    /// <summary>Initializes the view.</summary>
    public OptionalHardwarePlaceholderView()
    {
        Content = new Label { Text = "This optional-hardware tool is not implemented yet.", Margin = 20 };
    }
}
//

public partial class OptionalHardwarePlaceholderViewModel : OptionalHardwareBaseViewModel
{
    /// <inheritdoc />
    public override void Dispose()
    {
    }
}

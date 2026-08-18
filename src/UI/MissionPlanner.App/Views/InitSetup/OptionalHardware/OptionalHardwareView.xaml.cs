using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>Hosts the Optional Hardware workspace.</summary>
public partial class OptionalHardwareView : UraniumUI.Pages.UraniumContentPage
{
    /// <summary>Initializes the page.</summary>
    public OptionalHardwareView()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<OptionalHardwareViewModel>();
    }
}

using Avalonia.Controls;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Read-only vehicle information inside the shared diagnostics popup and detached window.</summary>
public partial class VehicleInfoView : UserControl
{
    /// <summary>Loads the view without sending commands or starting subscriptions.</summary>
    public VehicleInfoView()
    {
        InitializeComponent();
    }
}

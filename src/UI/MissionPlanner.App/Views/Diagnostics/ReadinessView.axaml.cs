using Avalonia.Controls;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Responsive typed readiness results inside the shared diagnostic host.</summary>
public partial class ReadinessView : UserControl
{
    /// <summary>Loads the presentation without opening subscriptions or sending commands.</summary>
    public ReadinessView() => InitializeComponent();
}

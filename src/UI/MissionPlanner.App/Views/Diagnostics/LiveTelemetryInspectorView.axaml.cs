using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Shared Inspector content with no drawer or window ownership.</summary>
public partial class LiveTelemetryInspectorView : UserControlViewBase<LiveTelemetryInspectorViewModel>
{
    /// <summary>Initializes the Inspector's reusable markup.</summary>
    public LiveTelemetryInspectorView()
    {
        InitializeComponent();
    }
}

using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Lists and downloads the dataflash logs stored on the vehicle.</summary>
public partial class DataFlashLogsView : UserControlViewBase<DataFlashLogsViewModel>
{
    /// <summary>Creates the view.</summary>
    public DataFlashLogsView()
    {
        InitializeComponent();
    }
}

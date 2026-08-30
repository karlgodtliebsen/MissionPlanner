using MissionPlanner.Avalonia.UI.Utilities;
using MissionPlanner.Avalonia.UI.ViewModels;

namespace MissionPlanner.Avalonia.UI.Views;

/// <summary>
/// The main window of the application.
/// </summary>
public partial class MainWindow : ExtendedWindow<MainViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
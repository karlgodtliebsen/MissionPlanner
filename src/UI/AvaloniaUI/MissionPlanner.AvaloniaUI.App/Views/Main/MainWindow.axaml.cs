using MissionPlanner.AvaloniaUI.App.Utilities;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Main;

/// <summary>
/// The main window of the application.
/// </summary>
public partial class MainWindow : WindowBase<MainViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async Task<bool> CanClose()
    {
        var result = await OverlayMessageBox.ShowAsync("Are you sure you want to exit?？", "Exit MissionPlanner Next Gen", button: MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }
}

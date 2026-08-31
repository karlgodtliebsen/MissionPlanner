using Ursa.Controls;
using Window = Avalonia.Controls.Window;

namespace MissionPlanner.AvaloniaUI.App.Views.Main;

/// <summary>
/// The splash window displayed at the start of the application.
/// </summary>
public partial class MainSplashWindow : SplashWindow
{
    /// <summary>
    /// 
    /// </summary>
    public MainSplashWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates the next window to be displayed after the splash screen.
    /// </summary>
    /// <returns>The next window to be displayed.</returns>
    protected override async Task<Window?> CreateNextWindow()
    {
        return new MainWindow()
        {
            DataContext = new MainViewModel()
        };
    }
}

using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.Advanced;

/// <summary>Displays the advanced setup workspace.</summary>
public partial class AdvancedPage : NavigationPage
{
    /// <summary>Initializes the advanced setup page.</summary>
    public AdvancedPage()
    {
        InitializeComponent();
        DataContext = ServiceHelper.GetRequiredService<AdvancedViewModel>();
    }
}

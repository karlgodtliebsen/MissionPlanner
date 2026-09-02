using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
        DataContext = ServiceHelper.GetRequiredService<MainShellViewModel>();
        var navigation = ServiceHelper.GetRequiredService<INavigationService>();
        navigation.Attach(NavigationHost, MainDrawer);
        AttachedToVisualTree += async (_, _) =>
            await navigation.NavigateAsync(MissionPlannerRoutes.FlightData);
    }
}

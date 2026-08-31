using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
        DataContext = ServiceHelper.GetRequiredService<MainShellViewModel>();
        var navigation = ServiceHelper.GetRequiredService<AvaloniaNavigationService>();
        navigation.Attach(NavigationHost, MainDrawer);
        AttachedToVisualTree += async (_, _) =>
            await navigation.NavigateAsync(MissionPlannerRoutes.FlightData);
    }
}

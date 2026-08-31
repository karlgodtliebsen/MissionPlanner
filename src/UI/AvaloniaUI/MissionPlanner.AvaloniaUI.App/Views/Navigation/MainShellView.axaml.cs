using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
        var navigation = ServiceHelper.GetRequiredService<AvaloniaNavigationService>();
        navigation.Attach(NavigationHost, MainDrawer);
    }
}

//public MainShellView(
//    AvaloniaNavigationService navigation)
//{
//    InitializeComponent();

//    navigation.Attach(
//        NavigationHost,
//        MainDrawer);
//}

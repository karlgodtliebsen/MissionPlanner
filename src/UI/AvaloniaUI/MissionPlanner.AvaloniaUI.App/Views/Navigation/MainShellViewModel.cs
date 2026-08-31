using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsNavigationOpen
    {
        get;
        set;
    }

    [RelayCommand]
    public void ToggleNavigation()
    {

    }

    [RelayCommand]
    public void Navigate()
    {

    }


}

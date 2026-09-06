using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Displays firmware discovery, validation, and installation workflows.</summary>
public partial class InstallFirmwarePage : NavigationViewBase<InstallFirmwareViewModel>
{
    /// <summary>Initializes the firmware page.</summary>
    public InstallFirmwarePage()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var item = MainTabControl.Items.First();
        MainTabControl.SelectedItem = item;
        MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
    }

    private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ViewModel.InvokeSelectionChanged(e);
    }

}

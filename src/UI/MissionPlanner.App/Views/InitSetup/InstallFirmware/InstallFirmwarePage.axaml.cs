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
        if (Design.IsDesignMode)
        {
            return;
        }
        var item = MainTabControl.Items.First();
        MainTabControl.SelectedItem = item;
        MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        MainTabControl.SelectionChanged -= MainTabControl_SelectionChanged;
        base.OnUnloaded(e);
    }

    private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ViewModel.InvokeSelectionChanged(e);
    }

}

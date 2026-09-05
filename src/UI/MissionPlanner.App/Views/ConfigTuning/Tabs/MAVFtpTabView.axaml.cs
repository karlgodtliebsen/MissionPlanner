using Avalonia.Controls;
using Avalonia.Input;
using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Displays the MAVFtpTabView configuration workflow.</summary>
public partial class MAVFtpTabView : NavigationViewBase<MavFtpTabViewModel>
{
    /// <summary>Initializes the MAVFtpTabView.</summary>
    public MAVFtpTabView()
    {
        InitializeComponent();
    }

    private async void OnEntriesDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Control { DataContext: Models.VehicleFileSystemEntryViewModel { IsDirectory: true } entry })
        {
            return;
        }

        ViewModel.SelectedEntry = entry;
        if (!ViewModel.OpenSelectedCommand.CanExecute(null))
        {
            return;
        }

        await ViewModel.OpenSelectedCommand.ExecuteAsync(null);
        e.Handled = true;
    }
}

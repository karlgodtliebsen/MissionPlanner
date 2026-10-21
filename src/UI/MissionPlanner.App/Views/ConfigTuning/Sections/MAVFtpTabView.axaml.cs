using Avalonia.Controls;
using Avalonia.Input;

namespace MissionPlanner.App.Views.ConfigTuning.Sections;

/// <summary>Displays the MAVFtpTabView configuration workflow.</summary>
public partial class MAVFtpTabView : NavigationViewBase<MavFtpTabViewModel>
{
    /// <summary>Initializes the MAVFtpTabView.</summary>
    public MAVFtpTabView()
    {
        InitializeComponent();
    }

    private void OnEntriesDoubleTapped(object? sender, TappedEventArgs e)
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

        // The command is asynchronous; mark the gesture handled before it yields.
        e.Handled = true;
        ViewModel.OpenSelectedCommand.Execute(null);
    }
}

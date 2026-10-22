using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

/// <summary>Shows the progress of a long-running operation.</summary>
public partial class ProgressDialogView : UserControl
{
    /// <summary>Creates the dialog view.</summary>
    public ProgressDialogView()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog view for the given view model.</summary>
    public ProgressDialogView(ProgressDialogViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}

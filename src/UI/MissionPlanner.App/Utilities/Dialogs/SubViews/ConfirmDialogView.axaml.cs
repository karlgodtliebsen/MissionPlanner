using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

/// <summary>Asks the user to confirm an action.</summary>
public partial class ConfirmDialogView : UserControl
{
    /// <summary>Creates the dialog view.</summary>
    public ConfirmDialogView()
    {
        InitializeComponent();
    }

    //public ConfirmDialogView(ConfirmDialogViewModel viewModel)
    //    : this()
    //{
    //    DataContext = viewModel;
    //}
}

using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

/// <summary>Lets the user pick one of several choices.</summary>
public partial class ChoiceDialogView : UserControl
{
    /// <summary>Creates the dialog view.</summary>
    public ChoiceDialogView()
    {
        InitializeComponent();
    }

    //public ChoiceDialogView(ChoiceDialogViewModel viewModel)
    //    : this()
    //{
    //    DataContext = viewModel;
    //}
}

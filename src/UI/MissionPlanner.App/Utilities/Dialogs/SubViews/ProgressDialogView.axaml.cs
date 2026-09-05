using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

public partial class ProgressDialogView : UserControl
{
    public ProgressDialogView()
    {
        InitializeComponent();
    }

    public ProgressDialogView(ProgressDialogViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}

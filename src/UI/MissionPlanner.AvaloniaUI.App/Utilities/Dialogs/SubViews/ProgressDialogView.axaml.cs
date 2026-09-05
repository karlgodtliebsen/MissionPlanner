using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews;

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

using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<MainShellViewModel>();
        DataContext = viewModel;
        AttachedToVisualTree += async (_, _) => await viewModel.InitializeAsync();
    }
}

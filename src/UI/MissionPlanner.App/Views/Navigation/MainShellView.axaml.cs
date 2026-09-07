using Avalonia.Controls;

namespace MissionPlanner.App.Views.Navigation;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            return;
        }
        var viewModel = ServiceHelper.GetRequiredService<MainShellViewModel>();
        DataContext = viewModel;
        AttachedToVisualTree += async (_, _) => await viewModel.InitializeAsync();
    }
}

using Avalonia.Controls;

namespace MissionPlanner.App.Views.Navigation;

/// <summary>The application shell: the navigation menu and the current page.</summary>
public partial class MainShellView : UserControl
{
    /// <summary>Creates the shell view.</summary>
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

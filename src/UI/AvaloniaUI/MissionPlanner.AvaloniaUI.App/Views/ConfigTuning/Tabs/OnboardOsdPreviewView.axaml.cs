using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>
/// 
/// </summary>
public partial class OnboardOsdPreviewView : UserControl
{
    private OnboardOsdTabViewModel? viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardOsdPreviewView"/> class.
    /// </summary>
    public OnboardOsdPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        viewModel?.LayoutChanged -= OnLayoutChanged;
        viewModel = DataContext as OnboardOsdTabViewModel;
        viewModel?.LayoutChanged += OnLayoutChanged;
        //TODO: PreviewCanvas is null here. Must be fixed
        PreviewCanvas.ViewModel = viewModel;
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        PreviewCanvas.InvalidateVisual();
    }

}

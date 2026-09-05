using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlanner.Library;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// 
/// </summary>
public partial class OnboardOsdPreviewView : UserControl
{
    private OnboardOsdTabViewModel? viewModel;
    private readonly OsdPreviewCanvas previewCanvas = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardOsdPreviewView"/> class.
    /// </summary>
    public OnboardOsdPreviewView()
    {
        InitializeComponent();
        previewCanvas = this.FindControl<OsdPreviewCanvas>("PreviewCanvas");
        DomainException.ThrowIfNull(previewCanvas, "The OSD preview canvas could not be loaded.");
        DataContextChanged += OnDataContextChanged;
        OnDataContextChanged(this, EventArgs.Empty);
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
        previewCanvas.InvalidateVisual();
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        previewCanvas.InvalidateVisual();
    }

}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

public partial class OnboardOsdPreviewView : UserControl
{
    private OnboardOsdTabViewModel? viewModel;

    public OnboardOsdPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (viewModel is not null) viewModel.LayoutChanged -= OnLayoutChanged;
        viewModel = DataContext as OnboardOsdTabViewModel;
        if (viewModel is not null) viewModel.LayoutChanged += OnLayoutChanged;
        PreviewCanvas.ViewModel = viewModel;
    }

    private void OnLayoutChanged(object? sender, EventArgs e) => PreviewCanvas.InvalidateVisual();
}

public sealed class OsdPreviewCanvas : Control
{
    public OnboardOsdTabViewModel? ViewModel { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Black, Bounds);
        var vm = ViewModel;
        if (vm is null || vm.PreviewGridWidth <= 0 || vm.PreviewGridHeight <= 0) return;

        var cellWidth = Bounds.Width / vm.PreviewGridWidth;
        var cellHeight = Bounds.Height / vm.PreviewGridHeight;
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#253040")), 1);
        for (var column = 0; column <= vm.PreviewGridWidth; column++)
            context.DrawLine(gridPen, new Point(column * cellWidth, 0), new Point(column * cellWidth, Bounds.Height));
        for (var row = 0; row <= vm.PreviewGridHeight; row++)
            context.DrawLine(gridPen, new Point(0, row * cellHeight), new Point(Bounds.Width, row * cellHeight));

        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
        foreach (var item in vm.PreviewItems.Where(item => item.IsEnabled))
        {
            if (item.Column < 0 || item.Column >= vm.PreviewGridWidth || item.Row < 0 || item.Row >= vm.PreviewGridHeight) continue;
            var selected = string.Equals(item.Key, vm.SelectedItem?.Key, StringComparison.Ordinal);
            var brush = selected ? Brushes.Orange : Brushes.Lime;
            var text = new FormattedText(item.Title, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, Math.Clamp(cellHeight * .65, 6, 16), brush);
            var origin = new Point(item.Column * cellWidth + 2, item.Row * cellHeight + 1);
            if (selected)
                context.DrawRectangle(null, new Pen(Brushes.Orange, 2),
                    new Rect(item.Column * cellWidth, item.Row * cellHeight, Math.Max(cellWidth, text.Width + 4), cellHeight));
            context.DrawText(text, origin);
        }
    }
}

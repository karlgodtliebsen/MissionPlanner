using Microsoft.Maui.Graphics;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Renders the selected onboard OSD layout as a character-grid preview.</summary>
public partial class OnboardOsdPreviewView : ContentView, IDrawable
{
    private OnboardOsdTabViewModel? viewModel;

    /// <summary>Initializes the platform-neutral OSD graphics preview.</summary>
    public OnboardOsdPreviewView()
    {
        InitializeComponent();
        PreviewCanvas.Drawable = this;
        BindingContextChanged += OnBindingContextChanged;
    }

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.Black;
        canvas.FillRectangle(dirtyRect);
        if (viewModel is null || viewModel.PreviewGridWidth <= 0 || viewModel.PreviewGridHeight <= 0)
        {
            return;
        }

        var cellWidth = dirtyRect.Width / viewModel.PreviewGridWidth;
        var cellHeight = dirtyRect.Height / viewModel.PreviewGridHeight;
        canvas.StrokeColor = Color.FromArgb("#253040");
        canvas.StrokeSize = 1;
        for (var column = 0; column <= viewModel.PreviewGridWidth; column++)
        {
            var x = dirtyRect.Left + column * cellWidth;
            canvas.DrawLine(x, dirtyRect.Top, x, dirtyRect.Bottom);
        }

        for (var row = 0; row <= viewModel.PreviewGridHeight; row++)
        {
            var y = dirtyRect.Top + row * cellHeight;
            canvas.DrawLine(dirtyRect.Left, y, dirtyRect.Right, y);
        }

        canvas.FontColor = Colors.Lime;
        canvas.FontSize = Math.Clamp(cellHeight * 0.65f, 6, 16);
        foreach (var item in viewModel.PreviewItems.Where(item => item.IsEnabled))
        {
            if (item.Column < 0 || item.Column >= viewModel.PreviewGridWidth ||
                item.Row < 0 || item.Row >= viewModel.PreviewGridHeight)
            {
                continue;
            }

            var x = dirtyRect.Left + item.Column * cellWidth + 2;
            var y = dirtyRect.Top + item.Row * cellHeight + 1;
            if (string.Equals(item.Key, viewModel.SelectedItem?.Key, StringComparison.Ordinal))
            {
                canvas.StrokeColor = Colors.Orange;
                canvas.StrokeSize = 2;
                canvas.DrawRectangle(
                    dirtyRect.Left + item.Column * cellWidth,
                    dirtyRect.Top + item.Row * cellHeight,
                    Math.Min(dirtyRect.Right - x, Math.Max(cellWidth, item.Title.Length * cellWidth)),
                    cellHeight);
                canvas.FontColor = Colors.Orange;
            }
            else
            {
                canvas.FontColor = Colors.Lime;
            }

            canvas.DrawString(item.Title, x, y, HorizontalAlignment.Left);
        }
    }

    private void OnBindingContextChanged(object? sender, EventArgs args)
    {
        if (viewModel is not null)
        {
            viewModel.LayoutChanged -= OnLayoutChanged;
        }

        viewModel = BindingContext as OnboardOsdTabViewModel;
        if (viewModel is not null)
        {
            viewModel.LayoutChanged += OnLayoutChanged;
        }

        PreviewCanvas.Invalidate();
    }

    private void OnLayoutChanged(object? sender, EventArgs args) => PreviewCanvas.Invalidate();
}

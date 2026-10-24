using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MissionPlanner.App.Views.ConfigTuning.Sections;

/// <summary>
/// Represents a custom control that renders a preview of the Onboard OSD layout. 
/// </summary>
public sealed class OsdPreviewCanvas : Control
{
    /// <summary>Identifies the view model used to render the character-grid preview.</summary>
    public static readonly StyledProperty<OnboardOsdTabViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<OsdPreviewCanvas, OnboardOsdTabViewModel?>(nameof(ViewModel));

    private OsdItemViewModel? dragged;
    private int offsetColumn;
    private int offsetRow;

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ViewModel is not { IsBusy: false, SelectedScreen: { } screen } vm ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var point = e.GetPosition(this);
        var column = (int)(point.X / Bounds.Width * vm.PreviewGridWidth);
        var row = (int)(point.Y / Bounds.Height * vm.PreviewGridHeight);
        var hit = vm.PreviewItems.LastOrDefault(item => item.IsEnabled && item.Row == row &&
            column >= item.Column && column < item.Column + PreviewText(item).Length);
        dragged = hit is null ? null : screen.Items.FirstOrDefault(item => item.Key == hit.Key);
        if (dragged is null)
        {
            return;
        }
        vm.SelectedItem = dragged;
        offsetColumn = column - dragged.Column;
        offsetRow = row - dragged.Row;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (dragged is null || ViewModel is not { IsBusy: false } vm ||
            vm.SelectedItem != dragged || e.Pointer.Captured != this || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var point = e.GetPosition(this);
        vm.PlaceSelected(Math.Clamp((int)(point.X / Bounds.Width * vm.PreviewGridWidth) - offsetColumn, 0, vm.PreviewGridWidth - 1),
            Math.Clamp((int)(point.Y / Bounds.Height * vm.PreviewGridHeight) - offsetRow, 0, vm.PreviewGridHeight - 1));
        e.Handled = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        dragged = null;
        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        dragged = null;
        base.OnPointerCaptureLost(e);
    }

    private static string PreviewText(MissionPlanner.Core.ConfigTuning.Osd.OsdPreviewItem item) => item.Key.ToUpperInvariant() switch
    {
        "BAT_VOLT" => "16.8V",
        "BATUSED" => "1234mAh",
        "CURRENT" => "12.3A",
        "ALTITUDE" => "123m",
        "SATS" => "GPS 14",
        "SPEED" or "GSPEED" => "12.3m/s",
        "FLTMODE" => "LOITER",
        "RSSI" => "RC 95%",
        "HOMEDIST" => "HOME 123m",
        "HEADING" => "123°",
        _ => item.Title
    };

    static OsdPreviewCanvas()
    {
        AffectsRender<OsdPreviewCanvas>(ViewModelProperty);
    }

    /// <summary>Gets or sets the OSD workspace projected by this canvas.</summary>
    public OnboardOsdTabViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Renders the OSD preview canvas.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var vm = ViewModel;
        if (vm is null || vm.PreviewGridWidth <= 0 || vm.PreviewGridHeight <= 0)
        {
            return;
        }

        var cellWidth = Bounds.Width / vm.PreviewGridWidth;
        var cellHeight = Bounds.Height / vm.PreviewGridHeight;
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#253040")), 1);
        for (var column = 0; column <= vm.PreviewGridWidth; column++)
        {
            context.DrawLine(gridPen, new Point(column * cellWidth, 0), new Point(column * cellWidth, Bounds.Height));
        }

        for (var row = 0; row <= vm.PreviewGridHeight; row++)
        {
            context.DrawLine(gridPen, new Point(0, row * cellHeight), new Point(Bounds.Width, row * cellHeight));
        }

        var typeface = new Typeface(new FontFamily("monospace"), FontStyle.Normal, FontWeight.Normal);
        foreach (var item in vm.PreviewItems.Where(item => item.IsEnabled))
        {
            if (item.Column < 0 || item.Column >= vm.PreviewGridWidth || item.Row < 0 || item.Row >= vm.PreviewGridHeight)
            {
                continue;
            }

            var selected = string.Equals(item.Key, vm.SelectedItem?.Key, StringComparison.Ordinal);
            var brush = selected ? Brushes.Orange : Brushes.Lime;
            var text = new FormattedText(PreviewText(item), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, Math.Clamp(cellHeight * .65, 6, 16), brush);
            var origin = new Point((item.Column * cellWidth) + 2, (item.Row * cellHeight) + 1);
            if (selected)
            {
                var availableWidth = Math.Max(0, Bounds.Width - origin.X);
                var selectionWidth = Math.Min(availableWidth, Math.Max(cellWidth, text.Width + 4));
                context.DrawRectangle(null, new Pen(Brushes.Orange, 2),
                    new Rect(item.Column * cellWidth, item.Row * cellHeight, selectionWidth, cellHeight));
            }

            context.DrawText(text, origin);
        }
    }
}

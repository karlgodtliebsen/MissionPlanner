using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlanner.App.Views.Common;

/// <summary>A bounded, passive line plot for setup diagnostics. Callers supply immutable snapshots.</summary>
public sealed class TelemetryLinePlot : Control
{
    /// <summary>Identifies the ordered X/Y samples to draw.</summary>
    public static readonly StyledProperty<IReadOnlyList<Point>?> SamplesProperty =
        AvaloniaProperty.Register<TelemetryLinePlot, IReadOnlyList<Point>?>(nameof(Samples));

    /// <summary>Identifies the axis and unit description.</summary>
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<TelemetryLinePlot, string>(nameof(Caption), "");

    static TelemetryLinePlot()
    {
        AffectsRender<TelemetryLinePlot>(SamplesProperty, CaptionProperty);
    }

    /// <summary>Gets or sets an ordered snapshot, limited to the latest 512 points when rendered.</summary>
    public IReadOnlyList<Point>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <summary>Gets or sets the plotted quantities and units.</summary>
    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var area = new Rect(58, 30, Math.Max(1, Bounds.Width - 76), Math.Max(1, Bounds.Height - 58));
        context.FillRectangle(new SolidColorBrush(Color.Parse("#17212B")), new Rect(Bounds.Size));
        DrawText(context, Caption, new Point(8, 5));
        var points = Samples?.TakeLast(512).Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).ToArray() ?? [];
        if (points.Length == 0)
        {
            DrawText(context, "Waiting for samples", new Point(65, 55));
            return;
        }

        var minX = points.Min(p => p.X);
        var maxX = Math.Max(minX + 0.001, points.Max(p => p.X));
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var padding = Math.Max(0.1, (maxY - minY) * .05);
        minY -= padding;
        maxY += padding;
        for (var line = 0; line <= 4; line++)
        {
            var y = area.Top + area.Height * line / 4;
            context.DrawLine(new Pen(Brushes.DimGray), new Point(area.Left, y), new Point(area.Right, y));
            DrawText(context, (maxY - (maxY - minY) * line / 4).ToString("G4", CultureInfo.InvariantCulture), new Point(3, y - 6));
        }
        DrawText(context, minX.ToString("G4", CultureInfo.InvariantCulture), new Point(area.Left, area.Bottom + 4));
        DrawText(context, maxX.ToString("G4", CultureInfo.InvariantCulture), new Point(area.Right - 40, area.Bottom + 4));
        Point Position(Point p) => new(area.Left + (p.X - minX) / (maxX - minX) * area.Width,
            area.Bottom - (p.Y - minY) / (maxY - minY) * area.Height);
        using (context.PushClip(area))
        {
            var pen = new Pen(Brushes.Orange, 2);
            for (var i = 1; i < points.Length; i++)
            {
                context.DrawLine(pen, Position(points[i - 1]), Position(points[i]));
            }
            context.DrawEllipse(Brushes.Orange, null, Position(points[^1]), 3, 3);
        }
    }

    private static void DrawText(DrawingContext context, string text, Point point)
    {
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11, Brushes.White), point);
    }
}

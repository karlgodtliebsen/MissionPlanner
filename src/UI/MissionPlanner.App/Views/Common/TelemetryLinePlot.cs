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

    /// <summary>Identifies the font size, in device-independent units, used by every plot label.</summary>
    public static readonly StyledProperty<double> EmSizeProperty =
        AvaloniaProperty.Register<TelemetryLinePlot, double>(nameof(EmSize), 11,
            validate: value => double.IsFinite(value) && value > 0);

    static TelemetryLinePlot()
    {
        AffectsRender<TelemetryLinePlot>(SamplesProperty, CaptionProperty, EmSizeProperty);
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

    /// <summary>Gets or sets the positive, finite font size for the caption, axes and empty-state text.</summary>
    public double EmSize
    {
        get => GetValue(EmSizeProperty);
        set => SetValue(EmSizeProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#17212B")), new Rect(Bounds.Size));
        using var clip = context.PushClip(new Rect(Bounds.Size));
        var caption = CreateText(Caption);
        caption.MaxTextWidth = Math.Max(1, Bounds.Width - 16);
        caption.MaxTextHeight = EmSize * 1.5;
        caption.Trimming = TextTrimming.CharacterEllipsis;
        context.DrawText(caption, new Point(8, 5));
        var plotTop = Math.Max(30, 5 + caption.Height + 12);
        var points = Samples?.TakeLast(512).Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).ToArray() ?? [];
        if (points.Length == 0)
        {
            context.DrawText(CreateText("Waiting for samples"), new Point(16, plotTop + EmSize));
            return;
        }

        var minX = points.Min(p => p.X);
        var maxX = Math.Max(minX + 0.001, points.Max(p => p.X));
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var padding = Math.Max(0.1, (maxY - minY) * .05);
        minY -= padding;
        maxY += padding;
        var yLabels = Enumerable.Range(0, 5)
            .Select(line => CreateText((maxY - (maxY - minY) * line / 4).ToString("G4", CultureInfo.InvariantCulture)))
            .ToArray();
        var firstX = CreateText(minX.ToString("G4", CultureInfo.InvariantCulture));
        var lastX = CreateText(maxX.ToString("G4", CultureInfo.InvariantCulture));
        plotTop = Math.Max(plotTop, 5 + caption.Height + 8 + yLabels.Max(label => label.Height) / 2);
        var plotLeft = Math.Max(58, yLabels.Max(label => label.Width) + 12);
        var plotBottom = Bounds.Height - Math.Max(firstX.Height, lastX.Height) - 12;
        var plotRight = Bounds.Width - 18;
        if (plotRight <= plotLeft || plotBottom <= plotTop)
        {
            return;
        }
        var area = new Rect(plotLeft, plotTop, plotRight - plotLeft, plotBottom - plotTop);
        for (var line = 0; line <= 4; line++)
        {
            var y = area.Top + area.Height * line / 4;
            context.DrawLine(new Pen(Brushes.DimGray), new Point(area.Left, y), new Point(area.Right, y));
            var label = yLabels[line];
            context.DrawText(label, new Point(area.Left - label.Width - 8, y - label.Height / 2));
        }
        context.DrawText(firstX, new Point(area.Left, area.Bottom + 4));
        if (lastX.Width + firstX.Width + 12 < area.Width)
        {
            context.DrawText(lastX, new Point(area.Right - lastX.Width, area.Bottom + 4));
        }
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

    private FormattedText CreateText(string text)
    {
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, EmSize, Brushes.White);
    }
}

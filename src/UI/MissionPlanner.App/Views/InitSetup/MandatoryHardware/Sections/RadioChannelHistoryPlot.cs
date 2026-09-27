using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Bounded UI-sampled receiver history. No controller writes or telemetry subscriptions.</summary>
public sealed class RadioChannelHistoryPlot : Control
{
    public static readonly StyledProperty<IEnumerable<RadioChannelDisplayViewModel>?> ChannelsProperty =
        AvaloniaProperty.Register<RadioChannelHistoryPlot, IEnumerable<RadioChannelDisplayViewModel>?>(nameof(Channels));
    public static readonly StyledProperty<string?> SessionKeyProperty =
        AvaloniaProperty.Register<RadioChannelHistoryPlot, string?>(nameof(SessionKey));
    public IEnumerable<RadioChannelDisplayViewModel>? Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }
    public string? SessionKey { get => GetValue(SessionKeyProperty); set => SetValue(SessionKeyProperty, value); }

    private readonly Queue<Sample> samples = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly DispatcherTimer timer;
    private int lastChannelCount;
    private const double WindowSeconds = 10;
    private sealed record Sample(double Time, Dictionary<int, int> Values);
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.Parse("#454B55"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#CDD3DD"));

    public RadioChannelHistoryPlot()
    {
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) => Capture();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Clear();
        timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SessionKeyProperty || change.Property == ChannelsProperty) Clear();
    }

    public void Clear()
    {
        samples.Clear();
        InvalidateVisual();
    }

    private void Capture()
    {
        if (!IsEffectivelyVisible)
        {
            Clear();
            return;
        }
        var now = clock.Elapsed.TotalSeconds;
        var count = Channels?.Count() ?? 0;
        if (count != lastChannelCount) { lastChannelCount = count; InvalidateMeasure(); }
        var values = (Channels ?? []).Where(channel => channel.HasSignal && !channel.IsStale)
            .ToDictionary(channel => channel.Number, channel => channel.Pwm);
        samples.Enqueue(new Sample(now, values));
        while (samples.Count > 201 || (samples.Count > 0 && samples.Peek().Time < now - WindowSeconds))
            samples.Dequeue();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 100 || Bounds.Height < 100) return;
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#202329")), null, new Rect(Bounds.Size), 6, 6);
        var plot = new Rect(48, 14, Bounds.Width - 60, 200);
        for (var pwm = 800; pwm <= 2200; pwm += 200)
        {
            var y = Y(pwm, plot);
            context.DrawLine(new Pen(GridBrush), new Point(plot.Left, y), new Point(plot.Right, y));
            Label(context, pwm.ToString(CultureInfo.InvariantCulture), 6, y - 7, LabelBrush);
        }
        for (var seconds = 0; seconds <= 10; seconds += 2)
        {
            var x = plot.Left + plot.Width * seconds / WindowSeconds;
            context.DrawLine(new Pen(GridBrush), new Point(x, plot.Top), new Point(x, plot.Bottom));
            Label(context, seconds == 10 ? "now" : $"{seconds - 10}s", Math.Min(x, plot.Right - 24), plot.Bottom + 5, LabelBrush);
        }
        var now = clock.Elapsed.TotalSeconds;
        var channels = (Channels ?? []).ToArray();
        using (context.PushClip(plot))
        {
            foreach (var channel in channels)
            {
                var pen = new Pen(new SolidColorBrush(Color.Parse(RadioChannelColors.For(channel.FunctionName, channel.Number))), 1.6);
                Point? previous = null;
                double previousTime = 0;
                foreach (var sample in samples)
                {
                    if (!sample.Values.TryGetValue(channel.Number, out var value))
                    {
                        previous = null;
                        continue;
                    }
                    var point = new Point(plot.Right - (now - sample.Time) / WindowSeconds * plot.Width, Y(value, plot));
                    if (previous is { } start && sample.Time - previousTime < 0.25)
                        context.DrawLine(pen, start, point);
                    previous = point;
                    previousTime = sample.Time;
                }
            }
        }
        if (samples.All(sample => sample.Values.Count == 0))
            Label(context, "Waiting for live RC input", plot.Left + 12, plot.Top + 12, LabelBrush);
        var columns = Math.Max(1, (int)(plot.Width / 165));
        for (var index = 0; index < channels.Length; index++)
        {
            var channel = channels[index];
            var x = plot.Left + index % columns * (plot.Width / columns);
            var y = plot.Bottom + 28 + index / columns * 19;
            var brush = new SolidColorBrush(Color.Parse(RadioChannelColors.For(channel.FunctionName, channel.Number)));
            context.DrawLine(new Pen(brush, 3), new Point(x, y + 7), new Point(x + 12, y + 7));
            var value = !channel.HasSignal ? "—" : channel.IsStale ? "stale" : channel.Pwm.ToString(CultureInfo.InvariantCulture);
            Label(context, $"CH{channel.Number} {channel.RoleLabel}  {value}", x + 17, y, LabelBrush);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        var columns = Math.Max(1, (int)((width - 60) / 165));
        return new Size(width, 255 + Math.Ceiling((Channels?.Count() ?? 0) / (double)columns) * 19);
    }

    private static double Y(int pwm, Rect plot) => plot.Bottom - Math.Clamp((pwm - 800) / 1400d, 0, 1) * plot.Height;
    private static void Label(DrawingContext context, string text, double x, double y, IBrush brush) =>
        context.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11, brush), new Point(x, y));
}

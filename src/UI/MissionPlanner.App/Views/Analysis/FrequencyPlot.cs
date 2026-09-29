using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MissionPlanner.Core.Analysis;

namespace MissionPlanner.App.Views.Analysis;

/// <summary>Renders completed measurements only; no FFT or filter computation occurs in this control.</summary>
public sealed class FrequencyPlot : Control
{
    /// <summary>Completed analysis property.</summary>
    public static readonly StyledProperty<FftWorkspaceResult?> ResultProperty =
        AvaloniaProperty.Register<FrequencyPlot, FftWorkspaceResult?>(nameof(Result));
    /// <summary>Time-frequency presentation selector.</summary>
    public static readonly StyledProperty<bool> ShowSpectrogramProperty =
        AvaloniaProperty.Register<FrequencyPlot, bool>(nameof(ShowSpectrogram));

    /// <summary>Completed immutable evidence.</summary>
    public FftWorkspaceResult? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }
    /// <summary>True for a heatmap, false for the mean spectrum.</summary>
    public bool ShowSpectrogram
    {
        get => GetValue(ShowSpectrogramProperty);
        set => SetValue(ShowSpectrogramProperty, value);
    }

    private static readonly IBrush[] heatColors = Enumerable.Range(0, 64).Select(i =>
        (IBrush)new SolidColorBrush(Color.FromRgb((byte)(i * 4), (byte)Math.Min(255, i * 6), (byte)(120 - i)))).ToArray();
    private double maximum = 1;
    private Rect Plot => new(58, 24, Math.Max(1, Bounds.Width - 78), Math.Max(1, Bounds.Height - 66));

    static FrequencyPlot()
    {
        AffectsRender<FrequencyPlot>(ResultProperty, ShowSpectrogramProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ResultProperty)
        {
            maximum = Math.Max(1e-12, Result?.Spectrogram.Frames.SelectMany(f => f.Bins).Max(b => b.Amplitude) ?? 1);
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var foreground = this.TryFindResource("ThemeForegroundBrush", out var resource) && resource is IBrush brush ? brush : Brushes.Gray;
        var plot = Plot;
        context.DrawRectangle(null, new Pen(foreground, 1), plot);
        if (Result is not { } result)
        {
            Label(context, "Open a log and analyze a uniform segment", plot.Left + 8, plot.Top + 12, foreground);
            return;
        }
        var frames = result.Spectrogram.Frames;
        var minimumHz = frames[0].Bins[0].FrequencyHz;
        var maximumHz = frames[0].Bins[^1].FrequencyHz;
        var span = Math.Max(result.Spectrum.ResolutionHz, maximumHz - minimumHz);
        if (ShowSpectrogram)
        {
            var columns = Math.Min(256, frames.Length);
            var rows = Math.Min(128, frames[0].Bins.Length);
            for (var x = 0; x < columns; x++)
            {
                var firstFrame = x * frames.Length / columns;
                var endFrame = (x + 1) * frames.Length / columns;
                for (var y = 0; y < rows; y++)
                {
                    var firstBin = y * frames[0].Bins.Length / rows;
                    var endBin = (y + 1) * frames[0].Bins.Length / rows;
                    var amplitude = 0.0;
                    for (var frame = firstFrame; frame < endFrame; frame++)
                    {
                        for (var bin = firstBin; bin < endBin; bin++)
                        {
                            amplitude = Math.Max(amplitude, frames[frame].Bins[bin].Amplitude);
                        }
                    }
                    var db = 20 * Math.Log10(Math.Max(1e-12, amplitude / maximum));
                    var color = heatColors[(int)Math.Clamp((db + 60) / 60 * 63, 0, 63)];
                    context.DrawRectangle(color, null, new Rect(plot.Left + x * plot.Width / columns,
                        plot.Bottom - (y + 1) * plot.Height / rows, plot.Width / columns + 0.5, plot.Height / rows + 0.5));
                }
            }
            Label(context, $"{maximumHz:F0} Hz", 0, plot.Top, foreground);
            Label(context, $"{minimumHz:F0} Hz", 0, plot.Bottom - 14, foreground);
            Label(context, $"{result.StartSeconds + frames[0].CenterTimeSeconds:F2}s", plot.Left, plot.Bottom + 8, foreground);
            Label(context, $"{result.StartSeconds + frames[^1].CenterTimeSeconds:F2}s", Math.Max(plot.Left, plot.Right - 75), plot.Bottom + 8, foreground);
            Label(context, $"Magnitude: −60…0 dB relative to {maximum:G3} {result.Source.Unit}", plot.Left, 2, foreground);
        }
        else
        {
            var maxAmplitude = Math.Max(1e-12, result.Spectrum.Bins.Where(b => b.FrequencyHz >= minimumHz && b.FrequencyHz <= maximumHz).Max(b => b.Amplitude));
            void Draw(IEnumerable<(double Hz, double Value)> values, IBrush color)
            {
                Point? previous = null;
                foreach (var value in values.Where(v => v.Hz >= minimumHz && v.Hz <= maximumHz))
                {
                    var point = new Point(plot.Left + (value.Hz - minimumHz) / span * plot.Width,
                        plot.Bottom - Math.Clamp(value.Value / maxAmplitude, 0, 1) * plot.Height);
                    if (previous is { } last)
                    {
                        context.DrawLine(new Pen(color, 1.5), last, point);
                    }
                    previous = point;
                }
            }
            Draw(result.Spectrum.Bins.Select(b => (b.FrequencyHz, b.Amplitude)), Brushes.MediumSeaGreen);
            Draw(result.CurrentSimulation.Select(b => (b.FrequencyHz, b.SimulatedAmplitude)), Brushes.DodgerBlue);
            Draw(result.ProposedSimulation.Select(b => (b.FrequencyHz, b.SimulatedAmplitude)), Brushes.DarkOrange);
            Label(context, $"{maxAmplitude:G3}", 0, plot.Top, foreground);
            Label(context, "0", 30, plot.Bottom - 14, foreground);
            Label(context, $"{minimumHz:F0} Hz", plot.Left, plot.Bottom + 8, foreground);
            Label(context, $"{maximumHz:F0} Hz", Math.Max(plot.Left, plot.Right - 75), plot.Bottom + 8, foreground);
            Label(context, $"Mean peak amplitude ({result.Source.Unit})", plot.Left, 2, foreground);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Result is not { } result || !Plot.Contains(e.GetPosition(this)))
        {
            return;
        }
        var point = e.GetPosition(this);
        var plot = Plot;
        var frames = result.Spectrogram.Frames;
        if (ShowSpectrogram)
        {
            var frameIndex = Math.Clamp((int)((point.X - plot.Left) / plot.Width * frames.Length), 0, frames.Length - 1);
            var frame = frames[frameIndex];
            var index = Math.Clamp((int)((plot.Bottom - point.Y) / plot.Height * frame.Bins.Length), 0, frame.Bins.Length - 1);
            var bin = frame.Bins[index];
            ToolTip.SetTip(this, $"Measured {result.StartSeconds + frame.CenterTimeSeconds:F4}s · {bin.FrequencyHz:F3} Hz · {bin.Amplitude:G5} {result.Source.Unit}");
        }
        else
        {
            var minimum = frames[0].Bins[0].FrequencyHz;
            var maximumHz = frames[0].Bins[^1].FrequencyHz;
            var hz = minimum + (point.X - plot.Left) / plot.Width * (maximumHz - minimum);
            var index = Math.Clamp((int)Math.Round(hz / result.Spectrum.ResolutionHz), 0, result.Spectrum.Bins.Length - 1);
            var bin = result.Spectrum.Bins[index];
            ToolTip.SetTip(this, $"Measured {bin.FrequencyHz:F3} Hz · {bin.Amplitude:G5} {result.Source.Unit}");
        }
    }

    private static void Label(DrawingContext context, string text, double x, double y, IBrush brush)
    {
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 12, brush), new Point(x, y));
    }
}

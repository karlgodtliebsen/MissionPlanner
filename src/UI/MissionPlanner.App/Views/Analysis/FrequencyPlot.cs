using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MissionPlanner.Core.Analysis;
using ScottPlot;
using ScottPlot.Avalonia;

namespace MissionPlanner.App.Views.Analysis;

/// <summary>ScottPlot presentation of completed FFT evidence; performs no signal analysis.</summary>
public sealed class FrequencyPlot : AvaPlot
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

    /// <summary>Initializes an interactive plot with a black background.</summary>
    public FrequencyPlot() => RebuildPlot();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ResultProperty || change.Property == ShowSpectrogramProperty)
            RebuildPlot();
    }

    private void RebuildPlot()
    {
        // Reset also releases the previous plot and removes heatmap colorbar panels.
        Reset();
        Plot.FigureBackground.Color = Colors.Black;
        Plot.DataBackground.Color = Colors.Black;
        Plot.Axes.Color(Colors.White);
        Plot.Grid.MajorLineColor = Color.FromHex("#303030");
        Plot.Legend.BackgroundColor = Colors.Black;
        Plot.Legend.FontColor = Colors.White;
        Plot.Legend.OutlineColor = Color.FromHex("#606060");
        ToolTip.SetTip(this, null);

        if (Result is not { } result || result.Spectrogram.Frames.IsEmpty)
        {
            Plot.Title("Open a log and select a source");
            Refresh();
            return;
        }
        var bins = result.Spectrogram.Frames[0].Bins;
        var minimumHz = bins[0].FrequencyHz;
        var maximumHz = bins[^1].FrequencyHz;
        if (ShowSpectrogram)
        {
            AddSpectrogram(result);
        }
        else
        {
            void AddCurve(IEnumerable<(double Hz, double Amplitude)> values, Color color, string label)
            {
                var selected = values.Where(v => v.Hz >= minimumHz && v.Hz <= maximumHz).ToArray();
                if (selected.Length == 0)
                    return;
                var curve = Plot.Add.Scatter(selected.Select(v => v.Hz).ToArray(), selected.Select(v => v.Amplitude).ToArray());
                curve.Color = color;
                curve.LineWidth = 1.5f;
                curve.MarkerSize = 0;
                curve.LegendText = label;
            }
            AddCurve(result.Spectrum.Bins.Select(b => (b.FrequencyHz, b.Amplitude)), Colors.MediumSeaGreen, "Measured");
            AddCurve(result.CurrentSimulation.Select(b => (b.FrequencyHz, b.SimulatedAmplitude)), Colors.DodgerBlue, "Current filters (simulated)");
            AddCurve(result.ProposedSimulation.Select(b => (b.FrequencyHz, b.SimulatedAmplitude)), Colors.DarkOrange, "Proposed filter (simulated)");
            Plot.Title("Frequency spectrum");
            Plot.XLabel("Frequency (Hz)");
            Plot.YLabel($"Mean peak amplitude ({result.Source.Unit})");
            Plot.ShowLegend();
            Plot.Axes.AutoScale();
            var upperHz = maximumHz > minimumHz ? maximumHz : minimumHz + result.Spectrum.ResolutionHz;
            var maximumAmplitude = result.Spectrum.Bins.Where(b => b.FrequencyHz >= minimumHz && b.FrequencyHz <= maximumHz).Max(b => b.Amplitude);
            maximumAmplitude = Math.Max(maximumAmplitude, result.CurrentSimulation.Select(b => b.SimulatedAmplitude).DefaultIfEmpty(0).Max());
            maximumAmplitude = Math.Max(maximumAmplitude, result.ProposedSimulation.Select(b => b.SimulatedAmplitude).DefaultIfEmpty(0).Max());
            Plot.Axes.SetLimits(minimumHz, upperHz, 0, Math.Max(1e-12, maximumAmplitude) * 1.05);
        }
        Refresh();
    }

    private void AddSpectrogram(FftWorkspaceResult result)
    {
        var frames = result.Spectrogram.Frames;
        var bins = frames[0].Bins;
        // Duplicate a single frame/bin only for display: ScottPlot heatmap extents
        // require two cell centers. Hover and exports still use original evidence.
        var columns = Math.Max(2, frames.Length);
        var rows = Math.Max(2, bins.Length);
        var maximum = Math.Max(1e-12, frames.SelectMany(f => f.Bins).Max(b => b.Amplitude));
        var intensities = new double[rows, columns];
        for (var column = 0; column < columns; column++)
        {
            var frame = frames[Math.Min(column, frames.Length - 1)];
            for (var row = 0; row < rows; row++)
                intensities[row, column] = Math.Clamp(20 * Math.Log10(Math.Max(1e-12, frame.Bins[Math.Min(row, bins.Length - 1)].Amplitude / maximum)), -60, 0);
        }
        var hop = (result.Spectrogram.Options.Fft.Size - result.Spectrogram.Options.OverlapSamples) / result.Source.SampleRateHz;
        var firstTime = result.StartSeconds + frames[0].CenterTimeSeconds;
        var lastTime = result.StartSeconds + frames[^1].CenterTimeSeconds;
        var minimumHz = bins[0].FrequencyHz;
        var maximumHz = bins[^1].FrequencyHz;
        var heatmap = Plot.Add.Heatmap(intensities);
        heatmap.FlipVertically = true;
        heatmap.Smooth = false;
        heatmap.Colormap = new ScottPlot.Colormaps.Turbo();
        heatmap.ManualRange = new ScottPlot.Range(-60, 0);
        heatmap.Position = new CoordinateRect(
            frames.Length == 1 ? firstTime - hop / 4 : firstTime,
            frames.Length == 1 ? firstTime + hop / 4 : lastTime,
            bins.Length == 1 ? minimumHz - result.Spectrum.ResolutionHz / 4 : minimumHz,
            bins.Length == 1 ? minimumHz + result.Spectrum.ResolutionHz / 4 : maximumHz);
        var colorbar = Plot.Add.ColorBar(heatmap);
        colorbar.Label = "Magnitude (dB relative)";
        colorbar.LabelStyle.ForeColor = Colors.White;
        colorbar.Axis.TickLabelStyle.ForeColor = Colors.White;
        Plot.Title($"Spectrogram · 0 dB = {maximum:G3} {result.Source.Unit}");
        Plot.XLabel("Time (log seconds)");
        Plot.YLabel("Frequency (Hz)");
        Plot.Axes.SetLimits(firstTime - hop / 2, lastTime + hop / 2,
            Math.Max(0, minimumHz - result.Spectrum.ResolutionHz / 2), maximumHz + result.Spectrum.ResolutionHz / 2);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Result is not { } result || result.Spectrogram.Frames.IsEmpty)
            return;
        var point = e.GetPosition(this);
        var coordinates = Plot.GetCoordinates(new Pixel((float)point.X * DisplayScale, (float)point.Y * DisplayScale));
        var frames = result.Spectrogram.Frames;
        if (ShowSpectrogram)
        {
            var hop = (result.Spectrogram.Options.Fft.Size - result.Spectrogram.Options.OverlapSamples) / result.Source.SampleRateHz;
            var frameIndex = Math.Clamp((int)Math.Round((coordinates.X - result.StartSeconds - frames[0].CenterTimeSeconds) / hop), 0, frames.Length - 1);
            var frame = frames[frameIndex];
            var binIndex = Math.Clamp((int)Math.Round((coordinates.Y - frame.Bins[0].FrequencyHz) / result.Spectrum.ResolutionHz), 0, frame.Bins.Length - 1);
            var bin = frame.Bins[binIndex];
            ToolTip.SetTip(this, $"Measured {result.StartSeconds + frame.CenterTimeSeconds:F4}s · {bin.FrequencyHz:F3} Hz · {bin.Amplitude:G5} {result.Source.Unit}");
        }
        else
        {
            var index = Math.Clamp((int)Math.Round(coordinates.X / result.Spectrum.ResolutionHz), 0, result.Spectrum.Bins.Length - 1);
            var bin = result.Spectrum.Bins[index];
            ToolTip.SetTip(this, $"Measured {bin.FrequencyHz:F3} Hz · {bin.Amplitude:G5} {result.Source.Unit}");
        }
    }
}
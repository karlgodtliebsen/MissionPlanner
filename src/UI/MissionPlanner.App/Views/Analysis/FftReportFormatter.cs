using System.Globalization;
using System.Text;
using MissionPlanner.Core.Analysis;

namespace MissionPlanner.App.Views.Analysis;

/// <summary>Formats bounded summaries while exports retain the full numerical evidence.</summary>
public static class FftReportFormatter
{
    /// <summary>Creates a readable measurement, interpretation and comparison summary.</summary>
    /// <param name="result">Completed analysis.</param>
    /// <returns>Plain text with clearly qualified assessments.</returns>
    public static string Format(FftWorkspaceResult result)
    {
        var text = new StringBuilder();
        text.AppendLine($"Measured: {result.Source.DisplayName}");
        text.AppendLine(FormattableString.Invariant($"Resolution {result.Spectrum.ResolutionHz:F3} Hz · amplitude {result.Source.Unit} · {result.Spectrogram.Frames.Length} windows"));
        text.AppendLine("\nPeaks (Hz · amplitude · harmonic membership)");
        foreach (var peak in result.Peaks.OrderByDescending(p => p.Amplitude).Take(40))
        {
            var membership = result.Harmonics.SelectMany(h => h.Harmonics)
                .Where(h => h.Peak.BinIndex == peak.BinIndex).Select(h => h.Order == 1 ? "Fundamental candidate" : $"Harmonic {h.Order}").Distinct();
            text.AppendLine(FormattableString.Invariant($"{peak.FrequencyHz:F2} Hz · {peak.Amplitude:G5} · {string.Join(", ", membership)}"));
        }
        if (result.Peaks.IsEmpty)
        {
            text.AppendLine("No peaks exceed the configured thresholds. This is not a mechanical health assessment.");
        }
        foreach (var series in result.Harmonics.Take(20))
        {
            text.AppendLine(FormattableString.Invariant($"Candidate harmonic series: {series.FundamentalHz:F2} Hz, {series.Harmonics.Length} members, heuristic confidence {series.Confidence:F2}. Consistent with periodic excitation; source undetermined."));
        }
        text.AppendLine("\nRPM evidence");
        if (result.Correlations.IsEmpty)
        {
            text.AppendLine("Insufficient overlapping actual RPM measurements for correlation.");
        }
        foreach (var item in result.Correlations)
        {
            text.AppendLine(FormattableString.Invariant($"{item.Source} {item.MotorIndex}: r={item.Correlation?.ToString("F3", CultureInfo.InvariantCulture) ?? "undefined"}, expected {item.ExpectedFrequencyHz:F2} Hz, observed {item.ObservedFrequencyHz:F2} Hz, RMS residual {item.RootMeanSquareErrorHz:F2} Hz ({item.Evidence.Length} pairs)."));
        }
        foreach (var candidate in result.Resonances)
        {
            text.AppendLine(FormattableString.Invariant($"Resonance candidate {candidate.MinimumFrequencyHz:F1}–{candidate.MaximumFrequencyHz:F1} Hz; peak {candidate.PeakFrequencyHz:F1} Hz; relative amplification {candidate.RelativeAmplification:F2}; heuristic confidence {candidate.Confidence:F2}. Excitation/load changes remain alternative explanations."));
        }
        if (!result.Comparison.IsEmpty)
        {
            text.AppendLine("\nBaseline comparison: frequency · baseline · current · change (largest 30 changes)");
            foreach (var row in result.Comparison.OrderByDescending(r => Math.Abs(r.Change)).Take(30))
            {
                text.AppendLine(FormattableString.Invariant($"{row.FrequencyHz:F2} Hz · {row.Baseline:G5} · {row.Current:G5} · {row.Change:+0.#####;-0.#####;0}"));
            }
        }
        foreach (var coverage in result.Coverage.Take(40))
        {
            text.AppendLine(FormattableString.Invariant($"{coverage.Peak.FrequencyHz:F2} Hz: {coverage.Assessment}"));
        }
        text.AppendLine("\nInterpretation limits");
        foreach (var note in result.Notes.Distinct())
        {
            text.AppendLine(note);
        }
        text.AppendLine("\nExport evidence to retain all bins, frames, correlations, comparisons and simulation values.");
        return text.ToString();
    }

    /// <summary>Formats copyable snapshot assignments using metadata-backed comments.</summary>
    /// <param name="snapshot">Captured read-only configuration.</param>
    /// <returns>Assignments plus comment-only diagnostics.</returns>
    public static string FormatParameters(NotchParameterSnapshot snapshot)
    {
        var text = new StringBuilder($"// {snapshot.Source}\n");
        foreach (var item in snapshot.Values)
        {
            var description = item.Description.Replace('\r', ' ').Replace('\n', ' ');
            text.AppendLine($"{item.Name} = {item.Value.ToString("R", CultureInfo.InvariantCulture)} // {description}");
        }
        foreach (var limitation in snapshot.Limitations)
        {
            text.AppendLine($"// {limitation}");
        }
        return text.ToString();
    }
}

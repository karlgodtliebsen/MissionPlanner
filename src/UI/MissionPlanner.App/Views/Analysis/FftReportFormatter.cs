using System.Globalization;
using System.Text;
using MissionPlanner.Core.Analysis;
using MissionPlanner.App.Presentation.Documents;

namespace MissionPlanner.App.Views.Analysis;

/// <summary>Formats bounded summaries while exports retain the full numerical evidence.</summary>
public static class FftReportFormatter
{
    /// <summary>Creates a styled Markdown measurement, interpretation and comparison summary.</summary>
    /// <param name="result">Completed analysis.</param>
    /// <returns>Markdown with escaped source text and qualified assessments.</returns>
    public static string Format(FftWorkspaceResult result)
    {
        var document = new UserDocumentBuilder().Heading("Vibration analysis")
            .Paragraph(result.Source.DisplayName)
            .Table("Measurement", "Value", new (string, string)[]
            {
                ("Frequency resolution", FormattableString.Invariant($"{result.Spectrum.ResolutionHz:F3} Hz")),
                ("Amplitude unit", result.Source.Unit),
                ("Analysis windows", result.Spectrogram.Frames.Length.ToString(CultureInfo.InvariantCulture)),
                ("Sample rate", FormattableString.Invariant($"{result.Source.SampleRateHz:F3} Hz"))
            });

        document.Heading("Measured peaks", 3);
        if (result.Peaks.IsEmpty)
        {
            document.Paragraph("No peaks exceed the configured thresholds. This is not a mechanical health assessment.");
        }
        else
        {
            document.Table("Frequency", "Amplitude and harmonic membership", result.Peaks.OrderByDescending(p => p.Amplitude).Take(40).Select(peak =>
            {
                var membership = result.Harmonics.SelectMany(h => h.Harmonics)
                    .Where(h => h.Peak.BinIndex == peak.BinIndex)
                    .Select(h => h.Order == 1 ? "Fundamental candidate" : $"Harmonic {h.Order}").Distinct();
                return (FormattableString.Invariant($"{peak.FrequencyHz:F2} Hz"),
                    FormattableString.Invariant($"{peak.Amplitude:G5} {result.Source.Unit} · {string.Join(", ", membership)}"));
            }));
        }
        if (!result.Harmonics.IsEmpty)
        {
            document.Heading("Harmonic candidates", 3);
            foreach (var series in result.Harmonics.Take(20))
                document.Bullet(FormattableString.Invariant($"{series.FundamentalHz:F2} Hz · {series.Harmonics.Length} members · heuristic confidence {series.Confidence:F2}. Consistent with periodic excitation; source undetermined."));
        }
        document.Heading("RPM evidence", 3);
        if (result.Correlations.IsEmpty)
            document.Paragraph("Insufficient overlapping actual RPM measurements for correlation.");
        foreach (var item in result.Correlations)
            document.Bullet(FormattableString.Invariant($"{item.Source} {item.MotorIndex}: r={item.Correlation?.ToString("F3", CultureInfo.InvariantCulture) ?? "undefined"}, expected {item.ExpectedFrequencyHz:F2} Hz, observed {item.ObservedFrequencyHz:F2} Hz, RMS residual {item.RootMeanSquareErrorHz:F2} Hz ({item.Evidence.Length} pairs)."));

        if (!result.Resonances.IsEmpty)
        {
            document.Heading("Resonance candidates", 3);
            foreach (var candidate in result.Resonances)
                document.Bullet(FormattableString.Invariant($"{candidate.MinimumFrequencyHz:F1}–{candidate.MaximumFrequencyHz:F1} Hz · peak {candidate.PeakFrequencyHz:F1} Hz · relative amplification {candidate.RelativeAmplification:F2} · heuristic confidence {candidate.Confidence:F2}. Excitation/load changes remain alternative explanations."));
        }
        if (!result.Comparison.IsEmpty)
        {
            document.Heading("Baseline comparison", 3).Paragraph("Largest 30 changes, in the selected signal's amplitude unit.")
                .Table("Frequency", "Baseline → current · change", result.Comparison.OrderByDescending(r => Math.Abs(r.Change)).Take(30)
                    .Select(row => (FormattableString.Invariant($"{row.FrequencyHz:F2} Hz"),
                        FormattableString.Invariant($"{row.Baseline:G5} → {row.Current:G5} · {row.Change:+0.#####;-0.#####;0}"))));
        }
        if (!result.Coverage.IsEmpty)
        {
            document.Heading("Filter coverage", 3);
            foreach (var coverage in result.Coverage.Take(40))
                document.Bullet(FormattableString.Invariant($"{coverage.Peak.FrequencyHz:F2} Hz: {coverage.Assessment}"));
        }
        document.Heading("Data quality and interpretation limits", 3);
        foreach (var note in result.Notes.Distinct())
            document.Bullet(note);
        document.Note("Export evidence to retain all bins, frames, correlations, comparisons and simulation values.");
        return document.Build("Vibration analysis").Markdown;
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

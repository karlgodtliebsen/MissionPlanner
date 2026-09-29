using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Vibration;

/// <summary>One time-aligned observation, with actual RPM separate from spectral frequency.</summary>
/// <param name="TimeSeconds">Source time.</param>
/// <param name="Rpm">Measured revolutions per minute, never motor-output percentage.</param>
/// <param name="ObservedFrequencyHz">Measured spectral peak frequency.</param>
/// <param name="Amplitude">Measured amplitude at that peak.</param>
public sealed record MotorFrequencyObservation(double TimeSeconds, double Rpm, double ObservedFrequencyHz, double Amplitude);

/// <summary>Evidence that a spectral band follows measured motor speed.</summary>
/// <param name="MotorIndex">Logged motor or RPM sensor index; mapping is supplied by the adapter.</param>
/// <param name="Source">Telemetry identity and limitations.</param>
/// <param name="Correlation">Pearson correlation, or null when speed/frequency has no variation.</param>
/// <param name="ExpectedFrequencyHz">Mean RPM/60 times the selected order.</param>
/// <param name="ObservedFrequencyHz">Mean observed frequency.</param>
/// <param name="DifferenceHz">Mean observed minus expected frequency.</param>
/// <param name="RootMeanSquareErrorHz">Frequency residual RMS.</param>
/// <param name="Order">Expected spectral order relative to shaft rotation.</param>
/// <param name="Evidence">All paired observations.</param>
public sealed record MotorFrequencyCorrelation(int MotorIndex, string Source, double? Correlation,
    double ExpectedFrequencyHz, double ObservedFrequencyHz, double DifferenceHz,
    double RootMeanSquareErrorHz, int Order, ImmutableArray<MotorFrequencyObservation> Evidence);

/// <summary>Computes correlation from explicitly time-aligned measurements.</summary>
public sealed class MotorFrequencyCorrelator
{
    /// <summary>Evaluates at least three finite pairs without assigning a mechanical cause.</summary>
    /// <param name="motorIndex">Motor or RPM sensor identity.</param>
    /// <param name="source">Source description.</param>
    /// <param name="observations">Time-aligned measured RPM and spectral peaks.</param>
    /// <param name="order">Positive integer shaft-frequency multiple.</param>
    /// <returns>Correlation and residual evidence; constant data has undefined correlation.</returns>
    public MotorFrequencyCorrelation Analyze(int motorIndex, string source,
        ImmutableArray<MotorFrequencyObservation> observations, int order = 1)
    {
        if (order < 1 || order > 32 || observations.IsDefault || observations.Length < 3 ||
            observations.Any(o => !double.IsFinite(o.TimeSeconds) || !double.IsFinite(o.Rpm) || o.Rpm <= 0 ||
                !double.IsFinite(o.ObservedFrequencyHz) || o.ObservedFrequencyHz <= 0 || !double.IsFinite(o.Amplitude) || o.Amplitude < 0))
        {
            throw new ArgumentException("At least three finite positive-frequency RPM pairs and order 1–32 are required.");
        }
        var expected = observations.Average(o => o.Rpm / 60 * order);
        var observed = observations.Average(o => o.ObservedFrequencyHz);
        var covariance = 0.0;
        var xx = 0.0;
        var yy = 0.0;
        var errors = 0.0;
        foreach (var item in observations)
        {
            var x = item.Rpm / 60 * order - expected;
            var y = item.ObservedFrequencyHz - observed;
            covariance += x * y;
            xx += x * x;
            yy += y * y;
            errors += Math.Pow(item.ObservedFrequencyHz - item.Rpm / 60 * order, 2);
        }
        return new MotorFrequencyCorrelation(motorIndex, source,
            xx > 0 && yy > 0 ? Math.Clamp(covariance / Math.Sqrt(xx * yy), -1, 1) : null,
            expected, observed, observed - expected, Math.Sqrt(errors / observations.Length), order, observations);
    }
}

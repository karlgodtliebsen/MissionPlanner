using System.Collections.Immutable;
using MissionPlanner.Analysis.Vibration;

namespace MissionPlanner.Core.Analysis;

/// <summary>One uniform, gap-free axis segment from a log.</summary>
/// <param name="Id">Unique segment selection key.</param>
/// <param name="Instance">Zero-based logged sensor instance.</param>
/// <param name="Signal">Gyro or Accel.</param>
/// <param name="Axis">Source axis.</param>
/// <param name="Unit">Physical amplitude unit.</param>
/// <param name="SampleRateHz">Measured regular rate or batch header rate.</param>
/// <param name="StartTimeSeconds">First sample on the log's boot-time clock.</param>
/// <param name="Samples">Immutable uniform values.</param>
/// <param name="BatchNumber">Batch identity when present.</param>
public sealed record ImuSampleSeries(string Id, int Instance, string Signal, VibrationAxis Axis, string Unit,
    double SampleRateHz, double StartTimeSeconds, ImmutableArray<double> Samples, int? BatchNumber)
{
    /// <summary>Final sample time, inclusive.</summary>
    public double EndTimeSeconds => StartTimeSeconds + ((Samples.Length - 1) / SampleRateHz);
    /// <summary>Number of available samples.</summary>
    public int SampleCount => Samples.Length;
    /// <summary>Selection label identifying the source and segment time.</summary>
    public string DisplayName => $"IMU {Instance} {Signal} {Axis} · {StartTimeSeconds:F2}–{EndTimeSeconds:F2}s · {SampleRateHz:F1} Hz · {SampleCount} samples";
}

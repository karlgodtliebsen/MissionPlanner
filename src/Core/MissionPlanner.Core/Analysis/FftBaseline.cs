using MissionPlanner.Analysis.Frequency;

namespace MissionPlanner.Core.Analysis;

/// <summary>A retained measured baseline with explicit units and provenance.</summary>
/// <param name="Name">Dataset label.</param>
/// <param name="Unit">Physical amplitude unit.</param>
/// <param name="Spectrum">Measured spectrum.</param>
public sealed record FftBaseline(string Name, string Unit, FrequencySpectrum Spectrum);
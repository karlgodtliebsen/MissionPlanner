namespace MissionPlanner.Core.Analysis;

/// <summary>Analysis choices captured before starting background computation.</summary>
/// <param name="StartSeconds">Inclusive source-log start time.</param>
/// <param name="EndSeconds">Inclusive source-log end time.</param>
/// <param name="FftSize">Power-of-two window length.</param>
/// <param name="MinimumHz">Lower retained spectrogram frequency.</param>
/// <param name="MaximumHz">Upper retained frequency, null for Nyquist.</param>
public sealed record FftAnalysisRequest(double StartSeconds, double EndSeconds, int FftSize, double MinimumHz, double? MaximumHz);
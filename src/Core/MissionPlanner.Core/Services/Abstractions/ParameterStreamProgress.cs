namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Progress information for parameter streaming.
/// </summary>
/// <param name="ReceivedCount">Number of parameters received so far.</param>
/// <param name="TotalCount">Total number of parameters (0 if not yet known).</param>
/// <param name="PercentComplete">Percentage complete (0-100).</param>
/// <param name="IsComplete">Whether all parameters have been received.</param>
public sealed record ParameterStreamProgress(int ReceivedCount, int TotalCount, int PercentComplete, bool IsComplete);

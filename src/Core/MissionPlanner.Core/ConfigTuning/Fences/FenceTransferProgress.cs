namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Reports fence transfer progress.</summary>
/// <param name="Stage">The transfer stage.</param>
/// <param name="Completed">The completed item count.</param>
/// <param name="Total">The total item count.</param>
public sealed record FenceTransferProgress(string Stage, int Completed, int Total);

namespace MissionPlanner.Core.Missions.Transfer;

/// <summary>Reports progress while downloading a typed MAVLink mission plan.</summary>
/// <param name="ReceivedItems">The number of unique items received.</param>
/// <param name="TotalItems">The item count advertised by the vehicle.</param>
/// <param name="RequestedSequence">The sequence currently being requested.</param>
public sealed record MissionDownloadProgress(int ReceivedItems, int TotalItems, ushort? RequestedSequence);

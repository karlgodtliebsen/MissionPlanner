namespace MissionPlanner.Core.Missions.Transfer;

public sealed record MissionUploadProgress(int SentItems, int TotalItems, ushort? RequestedSequence);

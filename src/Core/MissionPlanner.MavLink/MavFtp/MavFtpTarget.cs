using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpTarget.
/// </summary>
public sealed record MavFtpTarget(byte SystemId, byte ComponentId, TransportEndPoint EndPoint);

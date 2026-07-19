using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.MavFtp;

public sealed record MavFtpTarget(byte SystemId, byte ComponentId, TransportEndPoint EndPoint);

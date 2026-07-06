using MissionPlanner.Transport;

namespace MissionPlanner.Simulator.SmokeTests;

public sealed record TransportSmokeTestResult(
    int BytesReceived,
    byte[] Data,
    TransportEndPoint? RemoteEndpoint,
    DateTimeOffset ReceivedAt);

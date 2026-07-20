using MissionPlanner.Transport;

namespace MissionPlanner.Simulator.SmokeTests;

/// <summary>
/// Provides the public API for TransportSmokeTestResult.
/// </summary>
/// <param name="BytesReceived">The BytesReceived value.</param>
/// <param name="Data">The Data value.</param>
/// <param name="RemoteEndpoint">The RemoteEndpoint value.</param>
/// <param name="ReceivedAt">The ReceivedAt value.</param>
public sealed record TransportSmokeTestResult(
    int BytesReceived,
    byte[] Data,
    TransportEndPoint? RemoteEndpoint,
    DateTimeOffset ReceivedAt);

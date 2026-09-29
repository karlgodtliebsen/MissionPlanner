namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Saved per-transport dialog fields. Never log the URL or this record.</summary>
public sealed record PlannerNetworkDraft
{
    /// <summary>Remote hostname.</summary>
    public string? Host { get; init; }
    /// <summary>Remote or listener port.</summary>
    public string? Port { get; init; }
    /// <summary>Optional local bind address.</summary>
    public string Bind { get; init; } = "";
    /// <summary>Client's local port, including zero.</summary>
    public string LocalPort { get; init; } = "0";
    /// <summary>Non-secret WebSocket URL. Query-bearing URLs are remembered only in memory.</summary>
    public string Url { get; init; } = "ws://127.0.0.1:8765/";
    /// <inheritdoc />
    public override string ToString() => "Network connection draft";
}

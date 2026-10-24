namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>Explicit connection settings for a classic-CAN v0 management session.</summary>
public sealed record DroneCanConnectionOptions
{
    /// <summary>Gets the exclusive serial port for a direct SLCAN adapter.</summary>
    public string Port { get; init; } = "";
    /// <summary>Gets the distinct local reservation-table name for an isolated bench network.</summary>
    public string AllocationNetwork { get; init; } = "";
    /// <summary>Gets the SLCAN adapter's serial baud rate.</summary>
    public int Baud { get; init; } = 115200;
    /// <summary>Gets the CAN bus bit rate used when opening a direct adapter.</summary>
    public int CanBitRate { get; init; } = 1_000_000;
    /// <summary>Gets the autopilot CAN bus number, starting at one.</summary>
    public byte Bus { get; init; } = 1;
    /// <summary>Gets the maintenance node ID. IDs 126 and 127 are reserved for tools.</summary>
    public byte LocalNodeId { get; init; } = 126;
    /// <summary>Validates explicit settings before any connection is made.</summary>
    public void Validate(DroneCanTransportKind kind)
    {
        if (LocalNodeId is not (126 or 127) || Bus is < 1 or > 4 || Baud is < 9600 or > 2_000_000 ||
            kind == DroneCanTransportKind.DirectSlcan && (string.IsNullOrWhiteSpace(Port) || !SlcanFrameLink.BitRates.Contains(CanBitRate)))
            throw new ArgumentException("Select a serial port, supported CAN rate, bus 1–4 and maintenance node ID 126 or 127.");
    }
}

/// <summary>One bounded inspector record; payload bytes may contain node configuration values.</summary>
public sealed record DroneCanTrace(DateTimeOffset At, bool Transmitted, uint Identifier, string Data);

/// <summary>Owns a classic-CAN frame link and validates that its captured target remains usable.</summary>
public interface IDroneCanFrameLink : IAsyncDisposable
{
    /// <summary>Opens the captured route.</summary>
    Task OpenAsync(CancellationToken token);
    /// <summary>Receives the next extended CAN frame.</summary>
    Task<DroneCanFrame> ReadAsync(CancellationToken token);
    /// <summary>Transmits one extended CAN frame.</summary>
    Task SendAsync(DroneCanFrame frame, CancellationToken token);
    /// <summary>Rejects lost connections, stale heartbeats and armed autopilot targets.</summary>
    void Validate();
}

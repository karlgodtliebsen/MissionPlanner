using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.Replay;

/// <summary>PC recording status for the most recently opened connection.</summary>
/// <param name="State">Idle, Recording, Completed, or Error.</param>
/// <param name="FilePath">Storage identifier, if the first received frame created a log.</param>
/// <param name="Error">Recording failure or incomplete-log explanation.</param>
/// <param name="DroppedFrames">Frames omitted by the bounded observer.</param>
public sealed record TelemetryRecordingStatus(string State, string? FilePath, string? Error, long DroppedFrames = 0)
{
    /// <summary>Gets the connection recording start time.</summary>
    public DateTimeOffset? Started { get; init; }
    /// <summary>Gets bytes successfully written, including timestamps.</summary>
    public long BytesWritten { get; init; }
}

/// <summary>Publishes a PC recording lifecycle change independently of onboard logging.</summary>
public sealed class TelemetryRecordingChanged : DomainEvent<TelemetryRecordingStatus>
{
    /// <summary>Initializes a recording status change.</summary>
    public TelemetryRecordingChanged(TelemetryRecordingStatus status) : base(nameof(TelemetryRecordingChanged), status)
    {
    }

    /// <summary>Gets the new recording status.</summary>
    public TelemetryRecordingStatus Status => (TelemetryRecordingStatus)Payload!;
}

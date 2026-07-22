namespace MissionPlanner.Core.Firmware;

/// <summary>Provides a firmware update state change.</summary>
/// <param name="state">The new workflow state.</param>
/// <param name="status">The user-facing status.</param>
/// <param name="progress">Progress from zero to one.</param>
public sealed class FirmwareUpdateStateChangedEventArgs(FirmwareUpdateState state, string status, double progress) : EventArgs
{
    /// <summary>Gets the new state.</summary>
    public FirmwareUpdateState State { get; } = state;

    /// <summary>Gets the user-facing status.</summary>
    public string Status { get; } = status;

    /// <summary>Gets progress from zero to one.</summary>
    public double Progress { get; } = progress;
}

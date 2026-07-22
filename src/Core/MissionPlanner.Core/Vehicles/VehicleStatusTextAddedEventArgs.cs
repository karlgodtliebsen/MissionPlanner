namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Carries a status-text entry added to a bounded vehicle history.
/// </summary>
/// <param name="message">The added message.</param>
public sealed class VehicleStatusTextAddedEventArgs(VehicleStatusText message) : EventArgs
{
    /// <summary>Gets the added message.</summary>
    public VehicleStatusText Message { get; } = message;
}

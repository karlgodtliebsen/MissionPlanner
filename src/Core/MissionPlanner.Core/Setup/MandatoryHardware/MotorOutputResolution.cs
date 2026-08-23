namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents physical output resolution for one logical motor.</summary>
/// <param name="MotorNumber">The requested one-based logical motor number.</param>
/// <param name="Status">Whether the assignment is resolved, missing, or ambiguous.</param>
/// <param name="OutputChannels">All matching physical output channels in ascending order.</param>
public sealed record MotorOutputResolution(
    int MotorNumber,
    MotorOutputResolutionStatus Status,
    IReadOnlyList<int> OutputChannels)
{
    /// <summary>Gets the physical output when the assignment is unambiguous.</summary>
    public int? OutputChannel => Status == MotorOutputResolutionStatus.Resolved
        ? OutputChannels[0]
        : null;
}

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Reports confirmed writes and any recovery guidance after frame configuration.</summary>
/// <param name="Status">The terminal operation status.</param>
/// <param name="Message">The user-facing result.</param>
/// <param name="ConfirmedParameters">Parameters confirmed at their requested values.</param>
/// <param name="RollbackFailedParameters">Parameters that could not be confirmed at their original values.</param>
/// <param name="RequiresReboot">Whether any confirmed setting is metadata-marked as requiring reboot.</param>
public sealed record FrameConfigurationApplyResult(
    FrameConfigurationApplyStatus Status,
    string Message,
    IReadOnlyList<string> ConfirmedParameters,
    IReadOnlyList<string> RollbackFailedParameters,
    bool RequiresReboot)
{
    /// <summary>Gets whether all requested values were confirmed.</summary>
    public bool Succeeded => Status is FrameConfigurationApplyStatus.Succeeded or FrameConfigurationApplyStatus.NoChanges;
}

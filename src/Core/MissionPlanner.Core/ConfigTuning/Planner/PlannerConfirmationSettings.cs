namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures operation confirmation prompts.</summary>
public sealed record PlannerConfirmationSettings
{
    /// <summary>Gets whether vehicle parameter writes require confirmation.</summary>
    public bool ConfirmParameterWrites { get; init; } = true;

    /// <summary>Gets whether arm and disarm operations require confirmation.</summary>
    public bool ConfirmArmDisarm { get; init; } = true;

    /// <summary>Gets whether firmware changes require confirmation.</summary>
    public bool ConfirmFirmwareChanges { get; init; } = true;
}

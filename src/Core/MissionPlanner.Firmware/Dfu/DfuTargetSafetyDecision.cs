namespace MissionPlanner.Firmware.Dfu;

/// <summary>Identifies a DFU target-safety decision.</summary>
public enum DfuTargetSafetyDecision
{
    /// <summary>Known evidence and a remembered device association support the exact selection.</summary>
    Allowed,

    /// <summary>No known conflict exists, but board identity remains ambiguous.</summary>
    AllowedWithStrongWarning,

    /// <summary>Required evidence is absent or known evidence is incompatible.</summary>
    Blocked
}

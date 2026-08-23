namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes whether a logical motor has an unambiguous physical output assignment.</summary>
public enum MotorOutputResolutionStatus
{
    /// <summary>No servo output is assigned to the requested logical motor.</summary>
    NotAssigned,

    /// <summary>Exactly one physical output is assigned to the requested logical motor.</summary>
    Resolved,

    /// <summary>Multiple physical outputs are assigned to the same logical motor.</summary>
    Ambiguous
}

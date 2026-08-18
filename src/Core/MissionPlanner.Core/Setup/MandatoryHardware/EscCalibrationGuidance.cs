namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes whether and how ESC calibration applies to the connected vehicle.</summary>
/// <param name="Applicable">Whether the detected ESC protocol requires manual calibration.</param>
/// <param name="ProtocolName">The detected output protocol name.</param>
/// <param name="Explanation">A user-facing explanation of the calibration requirement.</param>
/// <param name="Steps">The guided calibration steps, empty when not applicable.</param>
public sealed record EscCalibrationGuidance(
    bool Applicable,
    string ProtocolName,
    string Explanation,
    IReadOnlyList<string> Steps);

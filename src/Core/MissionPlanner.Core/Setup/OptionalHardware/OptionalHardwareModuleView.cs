using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Represents one available optional-hardware module projection.</summary>
/// <param name="Key">The stable module key.</param>
/// <param name="Title">The module title.</param>
/// <param name="Description">The module description.</param>
/// <param name="Settings">The editable settings.</param>
/// <param name="Issues">The detected configuration issues.</param>
/// <param name="LiveStatus">An optional live status line.</param>
public sealed record OptionalHardwareModuleView(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<PeripheralSetting> Settings,
    IReadOnlyList<PeripheralValidationIssue> Issues,
    string? LiveStatus);

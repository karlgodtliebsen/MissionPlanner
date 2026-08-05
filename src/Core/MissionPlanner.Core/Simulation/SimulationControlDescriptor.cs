using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Describes one documented simulation control and its safety bounds.</summary>
/// <param name="Key">Stable control key.</param>
/// <param name="DisplayName">User-facing name.</param>
/// <param name="Description">Behavior and safety detail.</param>
/// <param name="Category">Control category.</param>
/// <param name="Unit">Display unit.</param>
/// <param name="Minimum">Minimum requested value.</param>
/// <param name="Maximum">Maximum requested value.</param>
/// <param name="RequiresConfirmation">Whether explicit hazardous-action confirmation is required.</param>
/// <param name="MaximumDuration">Maximum active duration before automatic reset.</param>
/// <param name="ParameterBindings">Ordered firmware parameter variants selected by live presence.</param>
/// <param name="SupportedFamilies">Supported firmware families.</param>
/// <param name="DocumentationUri">Official source documenting the control.</param>
public sealed record SimulationControlDescriptor(
    string Key,
    string DisplayName,
    string Description,
    SimulationControlCategory Category,
    string Unit,
    double Minimum,
    double Maximum,
    bool RequiresConfirmation,
    TimeSpan? MaximumDuration,
    IReadOnlyList<SimulationParameterBinding> ParameterBindings,
    IReadOnlySet<FirmwareFamily> SupportedFamilies,
    Uri DocumentationUri);

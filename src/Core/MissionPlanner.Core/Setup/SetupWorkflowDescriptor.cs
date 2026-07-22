using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Defines one setup workflow and its recommended dependencies.</summary>
/// <param name="Key">The stable workflow key.</param>
/// <param name="Title">The user-facing title.</param>
/// <param name="Description">The workflow purpose.</param>
/// <param name="SupportedFamilies">Firmware families known to support the workflow.</param>
/// <param name="Prerequisites">Recommended workflows that should normally be completed first.</param>
/// <param name="ConfigDestination">A related Config page title, when available.</param>
public sealed record SetupWorkflowDescriptor(
    SetupWorkflowKey Key,
    string Title,
    string Description,
    IReadOnlySet<FirmwareFamily> SupportedFamilies,
    IReadOnlyList<SetupWorkflowKey> Prerequisites,
    string? ConfigDestination);

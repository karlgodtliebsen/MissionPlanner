using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Describes how a pinned profile resolves to an installation.</summary>
/// <param name="State">Resolution state.</param>
/// <param name="Installation">Resolved installation, when available.</param>
/// <param name="Message">Resolution detail.</param>
public sealed record SitlInstallationResolution(
    SitlInstallationState State,
    SitlInstallation? Installation,
    string Message);

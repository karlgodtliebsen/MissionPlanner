namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Represents an opened OSD workspace over the shared parameter session.</summary>
/// <param name="Scope">The active vehicle and firmware scope.</param>
/// <param name="Session">The shared editing session.</param>
/// <param name="GlobalParameterNames">OSD parameters not owned by a numbered screen.</param>
/// <param name="Screens">The discovered screens.</param>
public sealed record OsdConfigurationWorkspace(
    ParameterEditScope Scope,
    IParameterEditSession Session,
    IReadOnlyList<string> GlobalParameterNames,
    IReadOnlyList<OsdScreenDefinition> Screens);

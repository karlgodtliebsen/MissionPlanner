using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Defines curated Basic Tuning groups for one firmware family.</summary>
/// <param name="Family">The firmware family.</param>
/// <param name="Groups">The ordered tuning groups.</param>
public sealed record BasicTuningProfile(
    FirmwareFamily Family,
    IReadOnlyList<BasicTuningGroupDefinition> Groups);

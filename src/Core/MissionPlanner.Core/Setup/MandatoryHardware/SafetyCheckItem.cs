using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects one assessed safety check.</summary>
/// <param name="Category">The safety category.</param>
/// <param name="Name">The check name.</param>
/// <param name="Status">The assessed status.</param>
/// <param name="Detail">A user-facing explanation.</param>
public sealed record SafetyCheckItem(string Category, string Name, SetupAssessmentStatus Status, string Detail);

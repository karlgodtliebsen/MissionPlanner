using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>A named, versioned set of parameter values.</summary>
public sealed record ParameterProfile(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    FirmwareFamily? FirmwareFamily,
    FirmwareSemanticVersion? FirmwareVersion,
    byte? MavType,
    string SourceIdentity,
    IReadOnlyDictionary<string, double> Values,
    IReadOnlyList<string> Tags,
    int FormatVersion = 1)
{
    /// <summary>The current persisted JSON schema version.</summary>
    public const int CurrentFormatVersion = 1;
}

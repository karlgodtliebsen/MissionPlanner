using System.Globalization;
using MissionPlanner.Core.ConfigTuning.Comparison;

namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>Default profile workflow built on the shared comparison and edit-session services.</summary>
public sealed class ParameterProfileService(IParameterComparisonService comparisons) : IParameterProfileService
{
    /// <inheritdoc />
    public ParameterProfile Create(
        IParameterEditSession session,
        string name,
        string? description = null,
        bool modifiedOnly = false,
        IReadOnlyCollection<string>? selectedNames = null,
        IReadOnlyList<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var selected = selectedNames?.ToHashSet(StringComparer.Ordinal);
        var fields = session.Fields.Where(field =>
            (!modifiedOnly || field.IsModified) &&
            (selected is null || selected.Contains(field.Name)));
        var values = fields.ToDictionary(field => field.Name, field => field.PendingValue, StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var firmware = session.Scope.FirmwareIdentity;
        return new ParameterProfile(
            Guid.NewGuid(), name.Trim(), description, now, now, firmware.Family,
            firmware.FlightVersion, firmware.MavType, session.VehicleId.ToString(), values,
            tags ?? []);
    }

    /// <inheritdoc />
    public ParameterProfileReview Review(ParameterProfile profile, IParameterEditSession session)
    {
        var firmware = session.Scope.FirmwareIdentity;
        var warnings = new List<string>();
        if (profile.FirmwareFamily is { } family && family != firmware.Family)
        {
            warnings.Add($"Profile firmware family {family} does not match {firmware.Family}.");
        }

        if (profile.FirmwareVersion is { } version && version != firmware.FlightVersion)
        {
            warnings.Add($"Profile firmware version {version} does not match {firmware.FlightVersion}.");
        }

        if (profile.MavType is { } mavType && mavType != firmware.MavType)
        {
            warnings.Add($"Profile vehicle/frame type {mavType} does not match {firmware.MavType}.");
        }

        var live = session.Fields.ToDictionary(
            field => field.Name,
            field => Input(field.Name, field.LiveValue),
            StringComparer.Ordinal);
        var right = profile.Values.ToDictionary(
            item => item.Key,
            item => Input(item.Key, item.Value),
            StringComparer.Ordinal);
        var metadata = session.Fields.ToDictionary(field => field.Name, field => field.Metadata, StringComparer.Ordinal);
        var comparison = comparisons.Compare(
            new ParameterComparisonSource("Live", session.VehicleId.ToString(), DateTimeOffset.UtcNow, firmware),
            live,
            new ParameterComparisonSource($"Profile: {profile.Name}", profile.SourceIdentity, profile.UpdatedAt, null),
            right,
            metadata);
        return new ParameterProfileReview(profile, comparison, warnings);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Stage(
        ParameterProfileReview review,
        IParameterEditSession session,
        IReadOnlyCollection<string> selectedNames) =>
        comparisons.Stage(review.Comparison, session, selectedNames);

    private static ParameterComparisonInput Input(string name, double value) =>
        new(name, value.ToString("R", CultureInfo.InvariantCulture));
}

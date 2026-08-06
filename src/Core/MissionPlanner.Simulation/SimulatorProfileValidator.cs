using MissionPlanner.Firmware;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation;

/// <summary>Validates simulator profile values and current host resources.</summary>
public sealed class SimulatorProfileValidator(ISimulatorHostEnvironment hostEnvironment) : ISimulatorProfileValidator
{
    private static readonly FirmwareFamily[] supportedFamilies =
    [
        FirmwareFamily.ArduCopter,
        FirmwareFamily.ArduPlane,
        FirmwareFamily.Rover,
        FirmwareFamily.ArduSub
    ];

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<SimulationValidationIssue>();
        if (profile.Id == Guid.Empty)
        {
            issues.Add(Issue("profile.id", "id", "The profile must have a stable identity."));
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            issues.Add(Issue("profile.name", "name", "Enter a profile name."));
        }

        if (!supportedFamilies.Contains(profile.FirmwareFamily))
        {
            issues.Add(Issue("profile.family", "firmwareFamily", "Select Copter, Plane, Rover, or Sub."));
        }

        if (string.IsNullOrWhiteSpace(profile.FrameModel))
        {
            issues.Add(Issue("profile.model", "frameModel", "Enter a frame or model supported by the selected runtime."));
        }

        ValidateLocation(profile.Location, issues);
        if (!double.IsFinite(profile.Speedup) || profile.Speedup is < 0.1 or > 1000)
        {
            issues.Add(Issue("profile.speedup", "speedup", "Speedup must be between 0.1 and 1000."));
        }

        if (profile.Endpoints.Count == 0)
        {
            issues.Add(Issue("profile.endpoints", "endpoints", "Configure at least one simulator endpoint."));
        }

        var duplicateNames = profile.Endpoints.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        foreach (var duplicate in duplicateNames)
        {
            issues.Add(Issue("profile.endpoint-name", "endpoints", $"Endpoint name '{duplicate.Key}' must be non-empty and unique."));
        }

        var duplicatePorts = profile.Endpoints.GroupBy(item => (item.Transport, item.Port))
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicatePorts)
        {
            issues.Add(Issue(
                "profile.port-duplicate",
                "endpoints",
                $"{duplicate.Key.Transport} port {duplicate.Key.Port} is assigned more than once."));
        }

        foreach (var endpoint in profile.Endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(endpoint.Host))
            {
                issues.Add(Issue("profile.endpoint-host", $"endpoints.{endpoint.Name}", "Endpoint host is required."));
            }

            if (endpoint.Port is < 1 or > 65535)
            {
                issues.Add(Issue("profile.port-range", $"endpoints.{endpoint.Name}", "Port must be between 1 and 65535."));
                continue;
            }

            if (!await hostEnvironment.IsPortAvailableAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                issues.Add(Issue(
                    "host.port-conflict",
                    $"endpoints.{endpoint.Name}",
                    $"{endpoint.Transport} port {endpoint.Port} is already in use."));
            }
        }

        var executableIssue = await hostEnvironment.ValidateExecutableAsync(
            profile.Binary.ExecutablePath,
            cancellationToken).ConfigureAwait(false);
        if (executableIssue is not null)
        {
            issues.Add(executableIssue);
        }

        if (profile.AdditionalArguments.Any(argument => argument is null))
        {
            issues.Add(Issue("profile.argument", "additionalArguments", "Argument tokens cannot be null."));
        }

        if (profile.Environment.Keys.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add(Issue("profile.environment", "environment", "Environment names cannot be empty."));
        }

        return issues;
    }

    private static void ValidateLocation(
        SimulationLocation location,
        ICollection<SimulationValidationIssue> issues)
    {
        if (!double.IsFinite(location.LatitudeDegrees) || location.LatitudeDegrees is < -90 or > 90)
        {
            issues.Add(Issue("profile.latitude", "location.latitudeDegrees", "Latitude must be between -90 and 90 degrees."));
        }

        if (!double.IsFinite(location.LongitudeDegrees) || location.LongitudeDegrees is < -180 or > 180)
        {
            issues.Add(Issue("profile.longitude", "location.longitudeDegrees", "Longitude must be between -180 and 180 degrees."));
        }

        if (!double.IsFinite(location.AltitudeMeters) || location.AltitudeMeters is < -1000 or > 100000)
        {
            issues.Add(Issue("profile.altitude", "location.altitudeMeters", "Altitude must be between -1000 and 100000 meters."));
        }

        if (!double.IsFinite(location.HeadingDegrees) || location.HeadingDegrees is < 0 or >= 360)
        {
            issues.Add(Issue("profile.heading", "location.headingDegrees", "Heading must be at least 0 and less than 360 degrees."));
        }
    }

    private static SimulationValidationIssue Issue(string code, string path, string message)
    {
        return new SimulationValidationIssue(code, path, message);
    }
}

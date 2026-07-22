using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Loads and persists versioned simulator profiles.</summary>
public sealed class SimulatorProfileService(
    ISimulatorProfileStore store,
    ILogger<SimulatorProfileService> logger) : ISimulatorProfileService
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private IReadOnlyList<SimulatorProfile> profiles = [];
    private bool initialized;

    /// <inheritdoc />
    public IReadOnlyList<SimulatorProfile> Profiles => profiles;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SimulatorProfile>> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return profiles;
        }

        var document = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(document))
        {
            try
            {
                var persisted = JsonSerializer.Deserialize<ProfileDocument>(document, jsonOptions);
                if (persisted is { Version: SchemaVersion } && persisted.Profiles.Count != 0 &&
                    persisted.Profiles.All(IsStructurallyValid))
                {
                    profiles = persisted.Profiles;
                    initialized = true;
                    return profiles;
                }

                logger.LogWarning("Simulator profiles had an unsupported schema or invalid structure; safe defaults will be used.");
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Simulator profile persistence was corrupt; safe defaults will be used.");
            }
        }

        profiles = [SimulatorProfile.CreateDefault()];
        initialized = true;
        await PersistAsync(cancellationToken).ConfigureAwait(false);
        return profiles;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!IsStructurallyValid(profile))
        {
            throw new ArgumentException("The simulator profile is structurally invalid.", nameof(profile));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        profiles = profiles.Where(item => item.Id != profile.Id).Append(profile)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var remaining = profiles.Where(item => item.Id != profileId).ToArray();
        profiles = remaining.Length == 0 ? [SimulatorProfile.CreateDefault()] : remaining;
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask PersistAsync(CancellationToken cancellationToken)
    {
        var document = JsonSerializer.Serialize(new ProfileDocument(SchemaVersion, profiles), jsonOptions);
        await store.WriteAsync(document, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsStructurallyValid(SimulatorProfile profile) =>
        profile.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(profile.Name) &&
        profile.Endpoints is not null &&
        profile.Binary is not null &&
        profile.AdditionalArguments is not null &&
        profile.Environment is not null;

    private sealed record ProfileDocument(
        [property: JsonPropertyName("schemaVersion")] int Version,
        IReadOnlyList<SimulatorProfile> Profiles);
}

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

    private static SimulationValidationIssue Issue(string code, string path, string message) =>
        new(code, path, message);
}

/// <summary>Provides read-only local host validation for simulator profiles.</summary>
public sealed class LocalSimulatorHostEnvironment : ISimulatorHostEnvironment
{
    /// <inheritdoc />
    public ValueTask<SimulationValidationIssue?> ValidateExecutableAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return ValueTask.FromResult<SimulationValidationIssue?>(new(
                "host.executable-required",
                "binary.executablePath",
                "Select an installed simulator executable."));
        }

        if (!Path.IsPathFullyQualified(executablePath))
        {
            return ValueTask.FromResult<SimulationValidationIssue?>(new(
                "host.executable-absolute",
                "binary.executablePath",
                "The simulator executable path must be absolute."));
        }

        if (!File.Exists(executablePath))
        {
            return ValueTask.FromResult<SimulationValidationIssue?>(new(
                "host.executable-missing",
                "binary.executablePath",
                $"Simulator executable was not found at '{executablePath}'."));
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                var mode = File.GetUnixFileMode(executablePath);
                const UnixFileMode executeBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                if ((mode & executeBits) == 0)
                {
                    return ValueTask.FromResult<SimulationValidationIssue?>(new(
                        "host.executable-permission",
                        "binary.executablePath",
                        "The selected simulator file does not have executable permission."));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return ValueTask.FromResult<SimulationValidationIssue?>(new(
                    "host.executable-access",
                    "binary.executablePath",
                    $"The selected simulator executable cannot be inspected: {exception.Message}"));
            }
        }

        return ValueTask.FromResult<SimulationValidationIssue?>(null);
    }

    /// <inheritdoc />
    public ValueTask<bool> IsPortAvailableAsync(
        SimulationEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var inUse = endpoint.Transport == SimulationEndpointTransport.Udp
            ? properties.GetActiveUdpListeners().Any(item => item.Port == endpoint.Port)
            : properties.GetActiveTcpListeners().Any(item => item.Port == endpoint.Port);
        return ValueTask.FromResult(!inUse);
    }
}

/// <summary>Explicitly reports that a launch adapter is not installed yet.</summary>
public sealed class UnavailableSimulatorRuntime : ISimulatorRuntime
{
    /// <inheritdoc />
    public string Name => "Unavailable";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SimulationValidationIssue> result =
        [
            new SimulationValidationIssue(
                "runtime.unavailable",
                "runtime",
                "No simulator launch runtime is installed. ArduPilot SITL runtime support is provided by Simulation step 03.")
        ];
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public Task<ISimulatorRuntimeSession> StartAsync(
        SimulatorStartRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No simulator launch runtime is installed.");
}

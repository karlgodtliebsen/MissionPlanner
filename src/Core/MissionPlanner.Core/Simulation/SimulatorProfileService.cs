using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Core.Simulation;

/// <summary>Loads and persists versioned simulator profiles.</summary>
public sealed class SimulatorProfileService(
    ISimulatorProfileStore store,
    ILogger<SimulatorProfileService> logger) : ISimulatorProfileService
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
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

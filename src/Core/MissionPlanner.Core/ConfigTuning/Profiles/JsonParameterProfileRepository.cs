using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>Stores each profile as an atomically replaced local JSON document.</summary>
public sealed class JsonParameterProfileRepository : IParameterProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string directory;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Creates a repository using the configured local directory.</summary>
    public JsonParameterProfileRepository(IOptions<ParameterProfileRepositoryOptions> options)
    {
        directory = string.IsNullOrWhiteSpace(options.Value.Directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MissionPlanner", "ParameterProfiles")
            : Path.GetFullPath(options.Value.Directory);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ParameterProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var profiles = new List<ParameterProfile>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            await using var stream = File.OpenRead(path);
            var profile = await ReadAsync(stream, cancellationToken).ConfigureAwait(false);
            profiles.Add(profile);
        }

        return profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <inheritdoc />
    public async Task<ParameterProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await ReadAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(ParameterProfile profile, CancellationToken cancellationToken = default)
    {
        Validate(profile);
        Directory.CreateDirectory(directory);
        var target = PathFor(profile.Id);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            gate.Release();
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = PathFor(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ParameterProfile> RenameAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var profile = await GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Parameter profile {id} does not exist.");
        var renamed = profile with { Name = name.Trim(), UpdatedAt = DateTimeOffset.UtcNow };
        await SaveAsync(renamed, cancellationToken).ConfigureAwait(false);
        return renamed;
    }

    /// <inheritdoc />
    public async Task<ParameterProfile> DuplicateAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var profile = await GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Parameter profile {id} does not exist.");
        var now = DateTimeOffset.UtcNow;
        var duplicate = profile with { Id = Guid.NewGuid(), Name = name.Trim(), CreatedAt = now, UpdatedAt = now };
        await SaveAsync(duplicate, cancellationToken).ConfigureAwait(false);
        return duplicate;
    }

    /// <inheritdoc />
    public async Task<ParameterProfile> ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var profile = await ReadAsync(source, cancellationToken).ConfigureAwait(false);
        await SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    /// <inheritdoc />
    public async Task ExportAsync(Guid id, Stream destination, CancellationToken cancellationToken = default)
    {
        var profile = await GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Parameter profile {id} does not exist.");
        await JsonSerializer.SerializeAsync(destination, profile, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private string PathFor(Guid id) => Path.Combine(directory, $"{id:N}.json");

    private static async Task<ParameterProfile> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        var profile = await JsonSerializer.DeserializeAsync<ParameterProfile>(source, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The parameter profile document is empty.");
        Validate(profile);
        return profile;
    }

    private static void Validate(ParameterProfile profile)
    {
        if (profile.FormatVersion != ParameterProfile.CurrentFormatVersion)
        {
            throw new InvalidDataException($"Unsupported parameter profile format version {profile.FormatVersion}.");
        }

        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidDataException("A profile requires an ID and name.");
        }

        if (profile.Values.Any(item => string.IsNullOrWhiteSpace(item.Key) || !double.IsFinite(item.Value)))
        {
            throw new InvalidDataException("Profile values require names and finite numeric values.");
        }
    }
}

using System.Text.Json;

namespace MissionPlanner.Maps.Custom;

/// <summary>Stores custom map sources in an atomic JSON document.</summary>
public sealed class JsonCustomMapSourceStore(string filePath) : ICustomMapSourceStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<CustomMapSourceSettings>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<CustomMapSourceSettings[]>(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false), Options) ?? [];
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(IReadOnlyList<CustomMapSourceSettings> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
        {
            CustomMapSourceValidator.ValidateAndThrow(source);
        }

        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var staging = fullPath + $".staging-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(staging, JsonSerializer.Serialize(sources.OrderBy(item => item.Id, StringComparer.Ordinal), Options), cancellationToken).ConfigureAwait(false);
            File.Move(staging, fullPath, true);
        }
        finally
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
        }
    }
}

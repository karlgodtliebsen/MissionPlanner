using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>
/// Bounded versioned atomic JSON repository.
/// </summary>
public sealed class JsonPoiRepository(IJsonPoiFilePathProvider provider, ILogger<JsonPoiRepository> logger) : IPoiRepository
{
    private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <inheritdoc />
    public async Task<IReadOnlyList<PointOfInterest>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = provider.GetPath();
        if (!File.Exists(path))
        {
            return [];
        }

        if (new FileInfo(path).Length > 4 * 1024 * 1024)
        {
            return await IsolateCorruptAsync(cancellationToken);
        }
        Debug.Print("Loading PointOfInterest from {0}", path);
        logger.LogDebug("Loading PointOfInterest from {path}", path);
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<Document>(text, options);
            return document?.SchemaVersion == 1 ? document.Items : await IsolateCorruptAsync(cancellationToken);
        }
        //catch the timeout exception and return an empty list
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "OperationCanceledException when Loading PointOfInterest from {path}", path);
            Debug.Print(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "UnauthorizedAccessException when Loading PointOfInterest from {path}", path);
            Debug.Print(ex.Message);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "IOException when Loading PointOfInterest from {path}", path);
            Debug.Print(ex.Message);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JsonException when Loading PointOfInterest from {path}", path);
            Debug.Print(ex.Message);
            return await IsolateCorruptAsync(cancellationToken);
        }
        return [];
    }

    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyList<PointOfInterest> items, CancellationToken cancellationToken = default)
    {
        if (items.Count > 10_000)
        {
            throw new InvalidDataException("POI count exceeds the 10,000 item limit.");
        }
        var path = provider.GetPath();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Document(1, items), options), cancellationToken);
        File.Move(temporary, path, true);
    }
    private Task<IReadOnlyList<PointOfInterest>> IsolateCorruptAsync(CancellationToken token)
    {
        var path = provider.GetPath();

        var backup = path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        File.Move(path, backup, true);
        return Task.FromResult<IReadOnlyList<PointOfInterest>>([]);
    }
    private sealed record Document(int SchemaVersion, IReadOnlyList<PointOfInterest> Items);
}

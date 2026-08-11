using System.Text.Json;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Stable local POI identity.</summary>
public readonly record struct PointOfInterestId(Guid Value) { /// <summary>Creates an identity.</summary>
    public static PointOfInterestId New() => new(Guid.NewGuid()); }
/// <summary>Persistent local planning point, never a MAVLink mission item.</summary>
public sealed record PointOfInterest(PointOfInterestId Id, string Name, GeoPosition Position, double? AltitudeMeters,
    string? Description, string? Category, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
/// <summary>Immutable POI collection state.</summary>
public sealed record PoiSnapshot(IReadOnlyList<PointOfInterest> Items, PointOfInterestId? SelectedId);
/// <summary>Persistent POI storage boundary.</summary>
public interface IPoiRepository
{
    /// <summary>Loads persisted POIs.</summary>
    Task<IReadOnlyList<PointOfInterest>> LoadAsync(CancellationToken cancellationToken = default);
    /// <summary>Atomically saves POIs.</summary>
    Task SaveAsync(IReadOnlyList<PointOfInterest> items, CancellationToken cancellationToken = default);
}
/// <summary>Owns validated local POIs.</summary>
public interface IPoiService
{
    /// <summary>Raised after local state changes.</summary>
    event EventHandler? Changed;
    /// <summary>Gets current state.</summary>
    PoiSnapshot Snapshot { get; }
    /// <summary>Loads persistent state once.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    /// <summary>Adds a POI.</summary>
    Task<PointOfInterest> AddAsync(string name, GeoPosition position, double? altitude, string? description, string? category, CancellationToken cancellationToken = default);
    /// <summary>Updates a POI.</summary>
    Task UpdateAsync(PointOfInterest item, CancellationToken cancellationToken = default);
    /// <summary>Deletes a POI.</summary>
    Task DeleteAsync(PointOfInterestId id, CancellationToken cancellationToken = default);
    /// <summary>Finds the closest POI to a geographic target.</summary>
    PointOfInterest? FindNearest(GeoPosition position);
}

/// <summary>Bounded versioned atomic JSON repository.</summary>
public sealed class JsonPoiRepository(string path) : IPoiRepository
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    /// <inheritdoc />
    public async Task<IReadOnlyList<PointOfInterest>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 4 * 1024 * 1024) return await IsolateCorruptAsync(cancellationToken);
        try { var document = JsonSerializer.Deserialize<Document>(await File.ReadAllTextAsync(path, cancellationToken), Options); return document?.SchemaVersion == 1 ? document.Items : await IsolateCorruptAsync(cancellationToken); }
        catch (JsonException) { return await IsolateCorruptAsync(cancellationToken); }
    }
    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyList<PointOfInterest> items, CancellationToken cancellationToken = default)
    {
        if (items.Count > 10_000) throw new InvalidDataException("POI count exceeds the 10,000 item limit.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Document(1, items), Options), cancellationToken);
        File.Move(temporary, path, true);
    }
    private Task<IReadOnlyList<PointOfInterest>> IsolateCorruptAsync(CancellationToken token)
    { var backup = path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"; File.Move(path, backup, true); return Task.FromResult<IReadOnlyList<PointOfInterest>>([]); }
    private sealed record Document(int SchemaVersion, IReadOnlyList<PointOfInterest> Items);
}

/// <summary>Default validated persistent POI service.</summary>
public sealed class PoiService(IPoiRepository repository) : IPoiService
{
    private IReadOnlyList<PointOfInterest> items = []; private bool initialized;
    /// <inheritdoc /> public event EventHandler? Changed;
    public event EventHandler? Changed;
    /// <inheritdoc /> public PoiSnapshot Snapshot => new(items, null);
    public PoiSnapshot Snapshot => new(items, null);
    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default) { if (initialized) return; items = (await repository.LoadAsync(cancellationToken)).Where(Valid).Take(10_000).ToArray(); initialized = true; Changed?.Invoke(this, EventArgs.Empty); }
    /// <inheritdoc />
    public async Task<PointOfInterest> AddAsync(string name, GeoPosition position, double? altitude, string? description, string? category, CancellationToken cancellationToken = default)
    { if (!position.IsValid || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("POI name and coordinates are required."); var now = DateTimeOffset.UtcNow; var item = new PointOfInterest(PointOfInterestId.New(), name.Trim()[..Math.Min(name.Trim().Length,128)], position, altitude, Limit(description), Limit(category), now, now); items = items.Append(item).ToArray(); await repository.SaveAsync(items, cancellationToken); Changed?.Invoke(this, EventArgs.Empty); return item; }
    /// <inheritdoc />
    public async Task UpdateAsync(PointOfInterest item, CancellationToken cancellationToken = default) { if (!Valid(item) || items.All(value => value.Id != item.Id)) throw new ArgumentException("POI is invalid or missing."); items = items.Select(value => value.Id == item.Id ? item with { UpdatedAt = DateTimeOffset.UtcNow } : value).ToArray(); await repository.SaveAsync(items, cancellationToken); Changed?.Invoke(this, EventArgs.Empty); }
    /// <inheritdoc />
    public async Task DeleteAsync(PointOfInterestId id, CancellationToken cancellationToken = default) { items = items.Where(item => item.Id != id).ToArray(); await repository.SaveAsync(items, cancellationToken); Changed?.Invoke(this, EventArgs.Empty); }
    /// <inheritdoc />
    public PointOfInterest? FindNearest(GeoPosition position) => items.OrderBy(item => Math.Pow(item.Position.LatitudeDegrees-position.LatitudeDegrees,2)+Math.Pow(item.Position.LongitudeDegrees-position.LongitudeDegrees,2)).FirstOrDefault();
    private static bool Valid(PointOfInterest item) => item.Position.IsValid && !string.IsNullOrWhiteSpace(item.Name) && item.Name.Length <= 128;
    private static string? Limit(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 1024)];
}

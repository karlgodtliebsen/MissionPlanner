using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Default validated persistent POI service.</summary>
public sealed class PoiService(IPoiRepository repository, ILogger<PoiService> logger) : IPoiService
{
    private IReadOnlyList<PointOfInterest> items = [];
    private bool isActive;

    /// <inheritdoc /> 
    public event Action? Changed;

    /// <inheritdoc />
    public PoiSnapshot Snapshot => new(items, null);

    /// <inheritdoc />
    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        if (isActive)
        {
            return;
        }

        isActive = true;
        try
        {
            items = (await repository.LoadAsync(cancellationToken)).Where(Valid).Take(10_000).ToArray();
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception when Activating PoiService");
            isActive = false;
        }

    }

    /// <inheritdoc />
    public async Task<PointOfInterest> AddAsync(string name, GeoPosition position, double? altitude, string? description, string? category, CancellationToken cancellationToken = default)
    {
        if (!position.IsValid || string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("POI name and coordinates are required.");
        }

        var now = DateTimeOffset.UtcNow;
        var item = new PointOfInterest(PointOfInterestId.New(), name.Trim()[..Math.Min(name.Trim().Length, 128)], position, altitude, Limit(description), Limit(category), now, now);
        items = items.Append(item).ToArray();
        await repository.SaveAsync(items, cancellationToken);

        Changed?.Invoke();
        return item;
    }
    /// <inheritdoc />
    public async Task UpdateAsync(PointOfInterest item, CancellationToken cancellationToken = default)
    {
        if (!Valid(item) || items.All(value => value.Id != item.Id))
        {
            throw new ArgumentException("POI is invalid or missing.");
        }

        items = items.Select(value => value.Id == item.Id ? item with { UpdatedAt = DateTimeOffset.UtcNow } : value).ToArray();
        await repository.SaveAsync(items, cancellationToken);
        Changed?.Invoke();
    }
    /// <inheritdoc />
    public async Task DeleteAsync(PointOfInterestId id, CancellationToken cancellationToken = default)
    {
        items = items.Where(item => item.Id != id).ToArray();
        await repository.SaveAsync(items, cancellationToken);
        Changed?.Invoke();
    }
    /// <inheritdoc />
    public PointOfInterest? FindNearest(GeoPosition position)
    {
        return items.OrderBy(item => Math.Pow(item.Position.LatitudeDegrees - position.LatitudeDegrees, 2) + Math.Pow(item.Position.LongitudeDegrees - position.LongitudeDegrees, 2)).FirstOrDefault();
    }

    private static bool Valid(PointOfInterest item)
    {
        return item.Position.IsValid && !string.IsNullOrWhiteSpace(item.Name) && item.Name.Length <= 128;
    }

    private static string? Limit(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 1024)];
    }
}

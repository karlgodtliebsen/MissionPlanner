using Mapsui.Layers;

namespace MissionPlanner.App.Maps;

/// <summary>Atomically replaces the single Mapsui basemap slot while preserving operational layers.</summary>
public sealed class MapBasemapController(Mapsui.Map map, IMapsuiBasemapFactory factory) : IDisposable
{
    private ILayer? current;
    private bool disposed;

    /// <summary>Gets the stable identifier of the active source.</summary>
    public string? CurrentSourceId { get; private set; }

    /// <summary>Raised after a source has been successfully replaced.</summary>
    public event EventHandler? BasemapChanged;

    /// <summary>Creates and atomically installs a source, retaining the current source on failure.</summary>
    /// <param name="sourceId">Catalog source identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the switch succeeded.</returns>
    public async ValueTask<bool> TrySwitchAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ILayer replacement;
        try
        {
            replacement = await factory.CreateAsync(sourceId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }

        var previous = current ?? map.Layers.FirstOrDefault(layer => layer.Name == MapsuiBasemapFactory.BasemapLayerName);
        map.Layers.Insert(0, replacement);
        if (previous is not null)
            map.Layers.Remove(previous);
        current = replacement;
        CurrentSourceId = sourceId;
        if (previous is IDisposable disposable)
            disposable.Dispose();
        BasemapChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (current is IDisposable disposable)
            disposable.Dispose();
        current = null;
    }
}

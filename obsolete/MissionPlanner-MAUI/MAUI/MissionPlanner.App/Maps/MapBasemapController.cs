using Mapsui.Layers;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.App.Maps;

/// <summary>Atomically replaces the single Mapsui basemap slot while preserving operational layers.</summary>
public sealed class MapBasemapController(Mapsui.Map map, IMapSourceResolver resolver, IMapsuiBasemapFactory factory, IMapUiDispatcher dispatcher) : IDisposable
{
    private readonly object gate = new();
    private ILayer? current;
    private CancellationTokenSource? activeRequest;
    private long generation;
    private bool disposed;

    /// <summary>Gets the stable identifier of the active source.</summary>
    public string? CurrentSourceId { get; private set; }

    /// <summary>Gets the renderer-neutral source committed to the basemap slot.</summary>
    public ResolvedMapSource? CurrentResolvedSource { get; private set; }

    /// <summary>Raised on the UI thread after a source has been successfully replaced.</summary>
    public event EventHandler? BasemapChanged;

    /// <summary>Creates and installs a source using last-request-wins semantics.</summary>
    public async ValueTask<MapBasemapSwitchResult> SwitchAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource request;
        long requestGeneration;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            requestGeneration = ++generation;
            activeRequest?.Cancel();
            request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            activeRequest = request;
        }

        ILayer? replacement = null;
        try
        {
            var resolution = await resolver.ResolveAsync(sourceId, request.Token).ConfigureAwait(false);
            if (!resolution.IsSuccess)
            {
                return IsCurrent(requestGeneration) ? new MapBasemapSwitchResult(MapBasemapSwitchStatus.ResolutionFailed, resolution.Message) : new MapBasemapSwitchResult(MapBasemapSwitchStatus.Superseded);
            }

            var creation = await factory.CreateAsync(resolution.Source!, request.Token).ConfigureAwait(false);
            if (!creation.IsSuccess)
            {
                return IsCurrent(requestGeneration) ? new MapBasemapSwitchResult(MapBasemapSwitchStatus.CreationFailed, creation.Message) : new MapBasemapSwitchResult(MapBasemapSwitchStatus.Superseded);
            }

            replacement = creation.Layer!;

            var committed = false;
            await dispatcher.InvokeAsync(() =>
            {
                ILayer? previous;
                lock (gate)
                {
                    if (disposed || generation != requestGeneration)
                    {
                        return;
                    }

                    previous = current ?? map.Layers.FirstOrDefault(layer => layer.Name == MapsuiBasemapFactory.BasemapLayerName);
                    map.Layers.Insert(0, replacement);
                    if (previous is not null)
                    {
                        map.Layers.Remove(previous);
                    }

                    current = replacement;
                    CurrentSourceId = sourceId;
                    CurrentResolvedSource = resolution.Source;
                    committed = true;
                }

                if (previous is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                BasemapChanged?.Invoke(this, EventArgs.Empty);
            }, request.Token).ConfigureAwait(false);

            if (!committed)
            {
                return new MapBasemapSwitchResult(MapBasemapSwitchStatus.Superseded);
            }

            replacement = null;
            return new MapBasemapSwitchResult(MapBasemapSwitchStatus.Success);
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested ? new MapBasemapSwitchResult(MapBasemapSwitchStatus.Cancelled) : new MapBasemapSwitchResult(MapBasemapSwitchStatus.Superseded);
        }
        finally
        {
            if (replacement is IDisposable disposable)
            {
                disposable.Dispose();
            }

            lock (gate)
            {
                if (ReferenceEquals(activeRequest, request))
                {
                    activeRequest = null;
                }
            }

            request.Dispose();
        }
    }

    private bool IsCurrent(long requestGeneration)
    {
        lock (gate)
        {
            return !disposed && generation == requestGeneration;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ILayer? layer;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            generation++;
            activeRequest?.Cancel();
            layer = current;
            current = null;
            CurrentResolvedSource = null;
        }

        if (layer is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

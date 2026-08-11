using Mapsui.Layers;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.App.Maps;

/// <summary>Identifies the outcome of an asynchronous basemap request.</summary>
public enum MapBasemapSwitchStatus
{
    /// <summary>The requested source was committed.</summary>
    Success,
    /// <summary>The source could not be resolved.</summary>
    ResolutionFailed,
    /// <summary>The renderer could not create the source.</summary>
    CreationFailed,
    /// <summary>A newer source request superseded this request.</summary>
    Superseded,
    /// <summary>The caller cancelled the request.</summary>
    Cancelled
}

/// <summary>Describes a basemap switch without using exceptions for expected failures.</summary>
/// <param name="Status">Switch outcome.</param>
/// <param name="Message">Optional diagnostic message.</param>
public sealed record MapBasemapSwitchResult(MapBasemapSwitchStatus Status, string? Message = null)
{
    /// <summary>Gets whether the requested source was committed.</summary>
    public bool IsSuccess => Status == MapBasemapSwitchStatus.Success;
}

/// <summary>Dispatches Mapsui mutations to the platform UI thread.</summary>
public interface IMapUiDispatcher
{
    /// <summary>Invokes an action on the UI thread.</summary>
    ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default);
}

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
                return IsCurrent(requestGeneration) ? new(MapBasemapSwitchStatus.ResolutionFailed, resolution.Message) : new(MapBasemapSwitchStatus.Superseded);
            var creation = await factory.CreateAsync(resolution.Source!, request.Token).ConfigureAwait(false);
            if (!creation.IsSuccess)
                return IsCurrent(requestGeneration) ? new(MapBasemapSwitchStatus.CreationFailed, creation.Message) : new(MapBasemapSwitchStatus.Superseded);
            replacement = creation.Layer!;

            var committed = false;
            await dispatcher.InvokeAsync(() =>
            {
                ILayer? previous;
                lock (gate)
                {
                    if (disposed || generation != requestGeneration)
                        return;
                    previous = current ?? map.Layers.FirstOrDefault(layer => layer.Name == MapsuiBasemapFactory.BasemapLayerName);
                    map.Layers.Insert(0, replacement);
                    if (previous is not null)
                        map.Layers.Remove(previous);
                    current = replacement;
                    CurrentSourceId = sourceId;
                    CurrentResolvedSource = resolution.Source;
                    committed = true;
                }
                if (previous is IDisposable disposable)
                    disposable.Dispose();
                BasemapChanged?.Invoke(this, EventArgs.Empty);
            }, request.Token).ConfigureAwait(false);

            if (!committed)
                return new(MapBasemapSwitchStatus.Superseded);
            replacement = null;
            return new(MapBasemapSwitchStatus.Success);
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested ? new(MapBasemapSwitchStatus.Cancelled) : new(MapBasemapSwitchStatus.Superseded);
        }
        finally
        {
            if (replacement is IDisposable disposable)
                disposable.Dispose();
            lock (gate)
            {
                if (ReferenceEquals(activeRequest, request))
                    activeRequest = null;
            }
            request.Dispose();
        }
    }

    private bool IsCurrent(long requestGeneration)
    {
        lock (gate)
            return !disposed && generation == requestGeneration;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ILayer? layer;
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            generation++;
            activeRequest?.Cancel();
            layer = current;
            current = null;
            CurrentResolvedSource = null;
        }
        if (layer is IDisposable disposable)
            disposable.Dispose();
    }
}

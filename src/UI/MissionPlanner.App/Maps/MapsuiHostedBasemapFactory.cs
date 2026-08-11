using BruTile;
using BruTile.Predefined;
using Mapsui.Layers;
using Mapsui.Tiling.Layers;
using MissionPlanner.Maps.Hosted;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.App.Maps;

/// <summary>Creates credential-gated hosted raster layers using the central map HTTP client.</summary>
public sealed class MapsuiHostedBasemapFactory(HostedMapSourceService hostedSources, IMapHttpClientFactory httpClientFactory)
{
    /// <summary>Creates a hosted layer only when its credential and policy gates pass.</summary>
    public async ValueTask<ILayer> CreateAsync(ResolvedMapSource source, CancellationToken cancellationToken = default)
    {
        var state = await hostedSources.GetStateAsync(source.Id, cancellationToken).ConfigureAwait(false);
        if (!state.IsSelectable)
            throw new HostedMapException(HostedMapFailureKind.MissingCredential, $"{state.Source.DisplayName} is disabled until its credential is configured.");
        var tileSource = new HostedTileSource(state, hostedSources, httpClientFactory.CreateClient());
        return new TileLayer(tileSource) { Name = CompositeMapsuiBasemapFactory.BasemapLayerName };
    }

    private sealed class HostedTileSource : ILocalTileSource, IDisposable
    {
        private readonly HostedMapSourceState state;
        private readonly HostedMapSourceService service;
        private readonly HttpClient client;

        public HostedTileSource(HostedMapSourceState state, HostedMapSourceService service, HttpClient client)
        {
            this.state = state;
            this.service = service;
            this.client = client;
            Schema = new GlobalSphericalMercator(state.Source.DisplayName, YAxis.OSM, state.Source.MinimumZoom, state.Source.MaximumZoom, state.Source.ContentFormat.ToString());
            Attribution = new Attribution(string.Join(" · ", state.Attributions.Select(item => item.Text)), string.Empty);
        }

        public ITileSchema Schema { get; }
        public string Name => state.Source.DisplayName;
        public Attribution Attribution { get; }

        public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
        {
            using var request = await service.CreateRequestAsync(state.Source.Id, tileInfo.Index.Level, tileInfo.Index.Col, tileInfo.Index.Row).ConfigureAwait(false);
            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw HostedMapSourceService.ClassifyFailure(new HttpRequestException(), response.StatusCode);
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
            catch (HostedMapException) { throw; }
            catch (Exception exception) { throw HostedMapSourceService.ClassifyFailure(exception); }
        }

        public void Dispose() => client.Dispose();
    }
}

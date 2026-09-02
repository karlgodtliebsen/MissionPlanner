using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using MissionPlanner.Core.ConfigTuning.Fences;
using NetTopologySuite.Geometries;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

public partial class GeoFenceMapView : UserControl, IDisposable
{
    private readonly Mapsui.Map map = new();
    private readonly MemoryLayer geometryLayer = new() { Name = "Fence geometry", Features = [] };
    private GeoFenceTabViewModel? viewModel;
    private bool disposed;

    public GeoFenceMapView()
    {
        InitializeComponent();
        // TODO: Route the fence basemap through the shared FlightData/MissionMap source resolver once the Config view owns an activation lifecycle.
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(geometryLayer);
        FenceMap.Map = map;
        FenceMap.MapTapped += OnMapTapped;
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        FenceMap.MapTapped -= OnMapTapped;
        DataContextChanged -= OnDataContextChanged;
        if (viewModel is not null) viewModel.GeometryChanged -= OnGeometryChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (viewModel is not null) viewModel.GeometryChanged -= OnGeometryChanged;
        viewModel = DataContext as GeoFenceTabViewModel;
        if (viewModel is not null) viewModel.GeometryChanged += OnGeometryChanged;
        Redraw();
    }

    private void OnGeometryChanged(object? sender, EventArgs e) => Redraw();

    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        var (longitude, latitude) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        viewModel?.HandleMapClick(latitude, longitude);
    }

    private void Redraw()
    {
        if (viewModel is null) { geometryLayer.Features = []; geometryLayer.DataHasChanged(); return; }
        var features = new List<IFeature>();
        if (viewModel.LocalPlan.ReturnPoint is { } returnPoint)
            features.Add(Point(returnPoint.LatitudeDegrees, returnPoint.LongitudeDegrees, Mapsui.Styles.Color.Blue));
        foreach (var area in viewModel.LocalPlan.Areas)
        {
            var color = area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.CircleInclusion
                ? Mapsui.Styles.Color.Green : Mapsui.Styles.Color.OrangeRed;
            if (area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.PolygonExclusion)
                features.Add(Line(area.Vertices.Select(v => (v.LatitudeDegrees, v.LongitudeDegrees)).ToList(), color, area.IsClosed));
            else if (area.Center is { } center)
                features.Add(Line(Circle(center.LatitudeDegrees, center.LongitudeDegrees, area.RadiusMeters), color, true));
        }
        geometryLayer.Features = features;
        geometryLayer.DataHasChanged();
        FenceMap.ForceUpdate();
        var extent = geometryLayer.Extent;
        if (extent is not null) map.Navigator.ZoomToBox(extent);
    }

    private static PointFeature Point(double latitude, double longitude, Mapsui.Styles.Color color)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        var feature = new PointFeature(new MPoint(x, y));
        feature.Styles.Add(new SymbolStyle { Fill = new Brush(color), Outline = new Pen(Mapsui.Styles.Color.White, 1) });
        return feature;
    }

    private static GeometryFeature Line(IReadOnlyList<(double Latitude, double Longitude)> positions, Mapsui.Styles.Color color, bool close)
    {
        var coordinates = positions.Select(p => { var (x, y) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude); return new Coordinate(x, y); }).ToList();
        if (close && coordinates.Count > 0) coordinates.Add(coordinates[0]);
        var feature = new GeometryFeature(new LineString(coordinates.ToArray()));
        feature.Styles.Add(new VectorStyle { Line = new Pen(color, 3) });
        return feature;
    }

    private static List<(double Latitude, double Longitude)> Circle(double latitude, double longitude, double radiusMeters)
    {
        const int segments = 48;
        const double metersPerDegree = 111_320;
        return Enumerable.Range(0, segments).Select(index =>
        {
            var angle = index * Math.Tau / segments;
            return (latitude + Math.Sin(angle) * radiusMeters / metersPerDegree,
                longitude + Math.Cos(angle) * radiusMeters / (metersPerDegree * Math.Max(.01, Math.Cos(latitude * Math.PI / 180))));
        }).ToList();
    }
}

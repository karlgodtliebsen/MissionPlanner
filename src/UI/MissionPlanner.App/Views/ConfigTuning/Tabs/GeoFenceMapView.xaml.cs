using BruTile.Predefined;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using MissionPlanner.Core.Configuration.Fences;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Dedicated Mapsui editor for fence polygons, circles, and return position.</summary>
public partial class GeoFenceMapView : ContentView, IDisposable
{
    private readonly Mapsui.Map map;
    private readonly List<Pin> pins = [];
    private readonly List<Polyline> lines = [];
    private GeoFenceTabViewModel? viewModel;

    /// <summary>Initializes the dedicated fence map and its graphics layer.</summary>
    public GeoFenceMapView()
    {
        InitializeComponent();
        map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        FenceMap.Map = map;
        FenceMap.MapClicked += OnMapClicked;
        BindingContextChanged += OnBindingContextChanged;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        FenceMap.MapClicked -= OnMapClicked;
        BindingContextChanged -= OnBindingContextChanged;
        if (viewModel is not null)
        {
            viewModel.GeometryChanged -= OnGeometryChanged;
        }
    }

    private void OnBindingContextChanged(object? sender, EventArgs args)
    {
        if (viewModel is not null)
        {
            viewModel.GeometryChanged -= OnGeometryChanged;
        }

        viewModel = BindingContext as GeoFenceTabViewModel;
        if (viewModel is not null)
        {
            viewModel.GeometryChanged += OnGeometryChanged;
        }

        Redraw();
    }

    private void OnGeometryChanged(object? sender, EventArgs args) => Redraw();

    private void OnMapClicked(object? sender, MapClickedEventArgs args) =>
        viewModel?.HandleMapClick(args.Point.Latitude, args.Point.Longitude);

    private void Redraw()
    {
        foreach (var pin in pins)
        {
            FenceMap.Pins.Remove(pin);
        }

        foreach (var line in lines)
        {
            FenceMap.Drawables.Remove(line);
        }

        pins.Clear();
        lines.Clear();
        if (viewModel is null)
        {
            return;
        }

        var positions = new List<Position>();
        if (viewModel.LocalPlan.ReturnPoint is { } returnPoint)
        {
            AddPin("Fence return", returnPoint.LatitudeDegrees, returnPoint.LongitudeDegrees, Colors.DodgerBlue, positions);
        }

        foreach (var area in viewModel.LocalPlan.Areas)
        {
            var inclusion = area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.CircleInclusion;
            var color = inclusion ? Colors.LimeGreen : Colors.OrangeRed;
            if (area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.PolygonExclusion)
            {
                var line = new Polyline { StrokeColor = color, StrokeWidth = area.IsClosed ? 4 : 2 };
                foreach (var vertex in area.Vertices)
                {
                    var position = new Position(vertex.LatitudeDegrees, vertex.LongitudeDegrees);
                    line.Positions.Add(position);
                    positions.Add(position);
                }

                if (area.IsClosed && area.Vertices.Count > 0)
                {
                    line.Positions.Add(new Position(area.Vertices[0].LatitudeDegrees, area.Vertices[0].LongitudeDegrees));
                }

                lines.Add(line);
                FenceMap.Drawables.Add(line);
            }
            else if (area.Center is { } center)
            {
                AddPin(area.Kind.ToString(), center.LatitudeDegrees, center.LongitudeDegrees, color, positions);
                var circle = CircleLine(center.LatitudeDegrees, center.LongitudeDegrees, area.RadiusMeters, color);
                lines.Add(circle);
                FenceMap.Drawables.Add(circle);
                positions.AddRange(circle.Positions);
            }
        }

        FenceMap.RefreshGraphics();
        Fit(positions);
    }

    private void AddPin(string label, double latitude, double longitude, Color color, ICollection<Position> positions)
    {
        var position = new Position(latitude, longitude);
        var pin = new Pin(FenceMap)
        {
            Label = label,
            Type = PinType.Pin,
            Color = color,
            Scale = 0.65f,
            Position = position
        };
        pins.Add(pin);
        positions.Add(position);
        FenceMap.Pins.Add(pin);
    }

    private static Polyline CircleLine(double latitude, double longitude, double radiusMeters, Color color)
    {
        const int segments = 48;
        const double metersPerDegree = 111_320;
        var line = new Polyline { StrokeColor = color, StrokeWidth = 4 };
        for (var index = 0; index <= segments; index++)
        {
            var angle = index * Math.Tau / segments;
            var latitudeDelta = Math.Sin(angle) * radiusMeters / metersPerDegree;
            var longitudeScale = Math.Max(0.01, Math.Cos(latitude * Math.PI / 180));
            var longitudeDelta = Math.Cos(angle) * radiusMeters / (metersPerDegree * longitudeScale);
            line.Positions.Add(new Position(latitude + latitudeDelta, longitude + longitudeDelta));
        }

        return line;
    }

    private void Fit(IReadOnlyList<Position> positions)
    {
        if (positions.Count == 0)
        {
            return;
        }

        if (positions.Count == 1)
        {
            var (singleX, singleY) = SphericalMercator.FromLonLat(positions[0].Longitude, positions[0].Latitude);
            map.Navigator.CenterOnAndZoomTo(new MPoint(singleX, singleY), 4.78);
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var position in positions)
        {
            var (x, y) = SphericalMercator.FromLonLat(position.Longitude, position.Latitude);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        var paddingX = Math.Max((maxX - minX) * 0.15, 50);
        var paddingY = Math.Max((maxY - minY) * 0.15, 50);
        map.Navigator.ZoomToBox(new MRect(minX - paddingX, minY - paddingY, maxX + paddingX, maxY + paddingY));
    }
}

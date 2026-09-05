using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>Renders terrain and planned-altitude profiles with Avalonia drawing primitives.</summary>
public sealed class MissionElevationProfileView : Control
{
    public MissionElevationProfile? Profile { get => GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    public static readonly StyledProperty<MissionElevationProfile?> ProfileProperty =
        AvaloniaProperty.Register<MissionElevationProfileView, MissionElevationProfile?>(nameof(Profile));

    static MissionElevationProfileView() => AffectsRender<MissionElevationProfileView>(ProfileProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x20, 0x24)), Bounds);
        if (Profile?.Samples.Count is not > 1) return;
        var values = Profile.Samples.SelectMany(x => new[] { x.TerrainElevationMeters, x.PlannedMslMeters }).OfType<double>().ToArray();
        if (values.Length == 0) return;
        var min = values.Min();
        var range = Math.Max(1, values.Max() - min);
        DrawLine(context, Profile.Samples.Select(x => (x.DistanceMeters, x.TerrainElevationMeters)), Brushes.SandyBrown, min, range);
        DrawLine(context, Profile.Samples.Select(x => (x.DistanceMeters, x.PlannedMslMeters)), Brushes.DeepSkyBlue, min, range);
    }

    private void DrawLine(DrawingContext context, IEnumerable<(double Distance, double? Value)> samples, IBrush brush, double min, double range)
    {
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            var started = false;
            foreach (var sample in samples.Where(x => x.Value is not null))
            {
                var point = new Point(10 + sample.Distance / Math.Max(1, Profile!.TotalDistanceMeters) * Math.Max(1, Bounds.Width - 20),
                    10 + Math.Max(1, Bounds.Height - 20) - (sample.Value!.Value - min) / range * Math.Max(1, Bounds.Height - 20));
                if (!started) { path.BeginFigure(point, false); started = true; } else path.LineTo(point);
            }
        }
        context.DrawGeometry(null, new Pen(brush, 2), geometry);
    }
}

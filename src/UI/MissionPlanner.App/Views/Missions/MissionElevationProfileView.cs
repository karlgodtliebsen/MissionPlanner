using Microsoft.Maui.Graphics;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.App.Views.Missions;

/// <summary>Lightweight cross-platform terrain and planned-altitude profile graph.</summary>
public sealed class MissionElevationProfileView : GraphicsView
{
    /// <summary>Profile rendered by the graph.</summary>
    public MissionElevationProfile? Profile { get => (MissionElevationProfile?)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    /// <summary>Bindable profile property.</summary>
    public static readonly BindableProperty ProfileProperty = BindableProperty.Create(nameof(Profile), typeof(MissionElevationProfile), typeof(MissionElevationProfileView), propertyChanged: OnProfileChanged);
    /// <summary>Creates the graph view.</summary>
    public MissionElevationProfileView() => Drawable = new ProfileDrawable();
    private static void OnProfileChanged(BindableObject bindable, object oldValue, object newValue)
    { var view = (MissionElevationProfileView)bindable; ((ProfileDrawable)view.Drawable).Profile = (MissionElevationProfile?)newValue; view.Invalidate(); }
    private sealed class ProfileDrawable : IDrawable
    {
        public MissionElevationProfile? Profile { get; set; }
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Color.FromArgb("#E6202024"); canvas.FillRectangle(dirtyRect);
            if (Profile?.Samples.Count is not > 1) return;
            var values = Profile.Samples.SelectMany(sample => new[] { sample.TerrainElevationMeters, sample.PlannedMslMeters }).OfType<double>().ToArray();
            if (values.Length == 0) return; var min = values.Min(); var range = Math.Max(1, values.Max() - min); var width = Math.Max(1, dirtyRect.Width - 20); var height = Math.Max(1, dirtyRect.Height - 20);
            DrawLine(canvas, Profile.Samples.Select(sample => (sample.DistanceMeters, sample.TerrainElevationMeters)), Colors.SandyBrown, min, range, width, height, Profile.TotalDistanceMeters);
            DrawLine(canvas, Profile.Samples.Select(sample => (sample.DistanceMeters, sample.PlannedMslMeters)), Colors.DeepSkyBlue, min, range, width, height, Profile.TotalDistanceMeters);
        }
        private static void DrawLine(ICanvas canvas, IEnumerable<(double Distance, double? Value)> values, Color color, double min, double range, double width, double height, double total)
        { canvas.StrokeColor = color; canvas.StrokeSize = 2; PathF? path = null; foreach (var sample in values) { if (sample.Value is null) { if (path is not null) canvas.DrawPath(path); path = null; continue; } var x = (float)(10 + sample.Distance / Math.Max(1, total) * width); var y = (float)(10 + height - (sample.Value.Value - min) / range * height); if (path is null) { path = new(); path.MoveTo(x,y); } else path.LineTo(x,y); } if (path is not null) canvas.DrawPath(path); }
    }
}

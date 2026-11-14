using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace MissionPlanner.App.Views.FlightData.Hud;

/// <summary>A lightweight, depth-sorted 3D attitude illustration, using the same renderer as the HUD.</summary>
public sealed class VehicleFrameCanvas : Control
{
    /// <summary>The selected vehicle's complete attitude and freshness.</summary>
    public static readonly StyledProperty<FrameAttitude> AttitudeProperty =
        AvaloniaProperty.Register<VehicleFrameCanvas, FrameAttitude>(nameof(Attitude));

    /// <summary>Gets or sets the displayed attitude.</summary>
    public FrameAttitude Attitude { get => GetValue(AttitudeProperty); set => SetValue(AttitudeProperty, value); }

    static VehicleFrameCanvas() => AffectsRender<VehicleFrameCanvas>(AttitudeProperty);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width > 0 && Bounds.Height > 0)
            context.Custom(new FrameDrawOperation(new Rect(Bounds.Size), Attitude));
    }

    private sealed class FrameDrawOperation(Rect bounds, FrameAttitude attitude) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;
        public bool HitTest(Point point) => Bounds.Contains(point);
        public bool Equals(ICustomDrawOperation? other) => other is FrameDrawOperation draw && draw.Bounds == Bounds && draw.attitude == attitude;
        private readonly FrameAttitude attitude = attitude;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;
            using var lease = feature.Lease();
            FramePainter.Draw(lease.SkCanvas, (float)Bounds.Width, (float)Bounds.Height, attitude);
        }
    }
}

internal static class FramePainter
{
    private sealed record Face(Vector3[] Vertices, SKColor Color)
    {
        public Vector3 Normal { get; } = Vector3.Normalize(Vector3.Cross(Vertices[1] - Vertices[0], Vertices[2] - Vertices[0]));
    }
    // Partition the static geometry once. A BSP traversal orders overlapping surfaces
    // correctly at every attitude, including the raised arrow and inverted frame.
    private static readonly SurfacePartition mesh = new(CreateMesh());
    private static readonly Vector3 light = Vector3.Normalize(new(-0.5f, -0.4f, -1));

    public static void Draw(SKCanvas canvas, float width, float height, FrameAttitude attitude)
    {
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, width, height));
        canvas.Clear(new SKColor(0x19, 0x20, 0x29));
        using var paint = new SKPaint { IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 11);
        // Fit the whole rotating airframe, including an inverted or vertical pose.
        var scale = Math.Max(0, Math.Min((width - 24) / 3.4f, (height - 48) / 3.4f));
        var center = new SKPoint(width / 2, height / 2 + 3);
        SKPoint Screen(Vector3 p) => new(center.X + p.X * scale, center.Y + p.Y * scale);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        paint.Color = new SKColor(0x32, 0x3D, 0x49);
        for (var i = -6; i <= 6; i++)
        {
            var offset = i * 0.5f;
            canvas.DrawLine(Screen(AttitudeProjection.WorldToCamera(new(-3, offset, .65f))),
                Screen(AttitudeProjection.WorldToCamera(new(3, offset, .65f))), paint);
            canvas.DrawLine(Screen(AttitudeProjection.WorldToCamera(new(offset, -3, .65f))),
                Screen(AttitudeProjection.WorldToCamera(new(offset, 3, .65f))), paint);
        }

        var rotation = new AttitudeProjection(attitude.Roll, attitude.Pitch, attitude.Yaw);
        using var path = new SKPath();
        foreach (var face in mesh.BackToFront(rotation))
        {
            var normal = rotation.BodyToWorld(face.Normal);
            var brightness = .48f + .52f * Math.Abs(Vector3.Dot(normal, light));
            var color = face.Color;
            if (!attitude.IsFresh)
            {
                var gray = (byte)((color.Red + color.Green + color.Blue) / 3);
                color = new SKColor(gray, gray, gray);
            }
            color = new SKColor((byte)(color.Red * brightness), (byte)(color.Green * brightness), (byte)(color.Blue * brightness));
            path.Reset();
            path.AddPoly(face.Vertices.Select(p => Screen(AttitudeProjection.WorldToCamera(rotation.BodyToWorld(p)))).ToArray(), true);
            paint.Style = SKPaintStyle.Fill;
            paint.Color = color;
            canvas.DrawPath(path, paint);
            // A fine material-colored seam keeps the frame readable at sidebar sizes.
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = .6f;
            canvas.DrawPath(path, paint);
        }

        paint.Style = SKPaintStyle.Fill;
        paint.Color = new SKColor(0xE2, 0xE8, 0xF0);
        canvas.DrawText("3D FRAME", 10, 17, SKTextAlign.Left, font, paint);
        paint.Color = attitude.IsFresh ? new SKColor(0x68, 0xDB, 0xB0) : new SKColor(0xFF, 0xC1, 0x66);
        canvas.DrawText(attitude.IsFresh ? "LIVE" : attitude.HasAttitude ? "STALE" : "NO ATTITUDE",
            width - 10, 17, SKTextAlign.Right, font, paint);
        paint.Color = new SKColor(0xC3, 0xCD, 0xD9);
        var angles = attitude.HasAttitude
            ? width < 280 ? $"R {attitude.Roll:0}°   P {attitude.Pitch:0}°   Y {attitude.Yaw:0}°"
                : $"Roll {attitude.Roll:0.0}°   Pitch {attitude.Pitch:0.0}°   Yaw {attitude.Yaw:0.0}°"
            : width < 280 ? "Waiting for attitude" : "Waiting for attitude telemetry";
        canvas.DrawText(angles, width / 2, height - 9, SKTextAlign.Center, font, paint);
        canvas.Restore();
    }

    private static Face[] CreateMesh()
    {
        var faces = new List<Face>();
        var carbon = new SKColor(0x57, 0x67, 0x7A);
        var edge = new SKColor(0x2D, 0x38, 0x48);
        var front = new SKColor(0x55, 0xDD, 0xC4);
        var rear = new SKColor(0xFF, 0x8D, 0x59);

        void Plate(Vector2[] outline, float top, float bottom, SKColor color)
        {
            faces.Add(new(outline.Select(p => new Vector3(p, top)).ToArray(), color));
            faces.Add(new(outline.Reverse().Select(p => new Vector3(p, bottom)).ToArray(), color));
            for (var i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                faces.Add(new([new(a, top), new(b, top), new(b, bottom), new(a, bottom)], color));
            }
        }

        void Cylinder(Vector2 center, float radius, float top, float bottom, SKColor color)
        {
            var outline = Enumerable.Range(0, 16).Select(i => center + radius *
                new Vector2(MathF.Cos(i * MathF.PI / 8), MathF.Sin(i * MathF.PI / 8))).ToArray();
            Plate(outline, top, bottom, color);
        }

        foreach (var x in new[] { -.85f, .85f })
        foreach (var y in new[] { -.85f, .85f })
        {
            var motor = new Vector2(x, y);
            var side = Vector2.Normalize(new Vector2(-y, x)) * .075f;
            Plate([side, motor + side, motor - side, -side], -.02f, .08f, carbon);
            Cylinder(motor, .18f, -.06f, .09f, edge);
            Cylinder(motor, .14f, -.22f, -.06f, new SKColor(0x43, 0x9A, 0xB5));
            // Stationary blades describe attitude only, not motor activity or commanded output.
            var angle = x * y > 0 ? .5f : -.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var across = new Vector2(-direction.Y, direction.X);
            var blade = Enumerable.Range(0, 24).Select(i => motor +
                direction * (.48f * MathF.Cos(i * MathF.PI / 12)) +
                across * (.075f * MathF.Sin(i * MathF.PI / 12))).ToArray();
            Plate(blade, -.255f, -.24f, x > 0 ? front : rear);
            Cylinder(motor, .055f, -.29f, -.255f, new SKColor(0xCF, 0xDF, 0xEB));
        }
        Vector2[] body = [new(.48f, -.22f), new(.3f, -.34f), new(-.34f, -.34f), new(-.48f, -.2f),
            new(-.48f, .2f), new(-.34f, .34f), new(.3f, .34f), new(.48f, .22f)];
        Plate(body, -.13f, .08f, edge);
        Plate(body.Select(p => p * .87f).ToArray(), -.18f, -.13f, carbon);
        // Raised nose arrow remains part of the airframe through every rotation.
        var gold = new SKColor(0xFF, 0xCE, 0x56);
        Plate([new(.37f, 0), new(.08f, -.17f), new(.08f, .17f)], -.205f, -.185f, gold);
        Plate([new(.08f, -.065f), new(-.27f, -.065f), new(-.27f, .065f), new(.08f, .065f)], -.205f, -.185f, gold);
        // Front camera and rear battery strap make the nose/tail recognizable side-on.
        Plate([new(.38f, -.12f), new(.55f, -.12f), new(.55f, .12f), new(.38f, .12f)], -.14f, .03f, front);
        Plate([new(-.25f, -.35f), new(-.13f, -.35f), new(-.13f, .35f), new(-.25f, .35f)], -.19f, -.18f, edge);
        return faces.ToArray();
    }

    private sealed class SurfacePartition
    {
        private const float Epsilon = .00001f;
        private readonly Vector3 normal;
        private readonly float distance;
        private readonly List<Face> coplanar = [];
        private readonly SurfacePartition? front, back;

        public SurfacePartition(IReadOnlyList<Face> faces)
        {
            // Sample candidates to avoid an unbalanced tree around the many motor facets.
            var splitter = faces.Where((_, i) => i % Math.Max(1, faces.Count / 16) == 0)
                .MinBy(candidate => SplitCost(candidate, faces))!;
            normal = splitter.Normal;
            distance = Vector3.Dot(normal, splitter.Vertices[0]);
            var positive = new List<Face>();
            var negative = new List<Face>();
            foreach (var face in faces)
            {
                var sides = face.Vertices.Select(p => Vector3.Dot(normal, p) - distance).ToArray();
                var hasFront = sides.Any(d => d > Epsilon);
                var hasBack = sides.Any(d => d < -Epsilon);
                if (!hasFront && !hasBack) coplanar.Add(face);
                else if (!hasBack) positive.Add(face);
                else if (!hasFront) negative.Add(face);
                else
                {
                    var a = new List<Vector3>();
                    var b = new List<Vector3>();
                    for (var i = 0; i < sides.Length; i++)
                    {
                        var j = (i + 1) % sides.Length;
                        var p = face.Vertices[i];
                        if (sides[i] >= -Epsilon) a.Add(p);
                        if (sides[i] <= Epsilon) b.Add(p);
                        if ((sides[i] > Epsilon && sides[j] < -Epsilon) || (sides[i] < -Epsilon && sides[j] > Epsilon))
                        {
                            var intersection = Vector3.Lerp(p, face.Vertices[j], sides[i] / (sides[i] - sides[j]));
                            a.Add(intersection);
                            b.Add(intersection);
                        }
                    }
                    positive.Add(new(a.ToArray(), face.Color));
                    negative.Add(new(b.ToArray(), face.Color));
                }
            }
            if (positive.Count > 0) front = new(positive);
            if (negative.Count > 0) back = new(negative);
        }

        public IEnumerable<Face> BackToFront(AttitudeProjection rotation)
        {
            var cameraBehind = AttitudeProjection.WorldToCamera(rotation.BodyToWorld(normal)).Z > 0;
            var far = cameraBehind ? front : back;
            var near = cameraBehind ? back : front;
            if (far is not null)
                foreach (var face in far.BackToFront(rotation)) yield return face;
            foreach (var face in coplanar) yield return face;
            if (near is not null)
                foreach (var face in near.BackToFront(rotation)) yield return face;
        }

        private static int SplitCost(Face plane, IReadOnlyList<Face> faces)
        {
            var offset = Vector3.Dot(plane.Normal, plane.Vertices[0]);
            var positive = 0;
            var negative = 0;
            var split = 0;
            foreach (var face in faces)
            {
                var hasFront = false;
                var hasBack = false;
                foreach (var p in face.Vertices)
                {
                    var side = Vector3.Dot(plane.Normal, p) - offset;
                    hasFront |= side > Epsilon;
                    hasBack |= side < -Epsilon;
                }
                if (hasFront) positive++;
                if (hasBack) negative++;
                if (hasFront && hasBack) split++;
            }
            return split * 3 + Math.Abs(positive - negative);
        }
    }
}

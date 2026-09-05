using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace MissionPlanner.App.Views.FlightData.Hud;

/// <summary>Renders the flight HUD through Avalonia's native Skia drawing lease.</summary>
public sealed class HudCanvas : Control
{
    private HudSnapshot snapshot;

    /// <summary>Copies the current telemetry and schedules a visual refresh.</summary>
    public void Update(HudViewModel viewModel)
    {
        snapshot = new HudSnapshot(viewModel.Pitch, viewModel.Roll, viewModel.Heading,
            viewModel.AirSpeed, viewModel.GroundSpeed, viewModel.Altitude,
            viewModel.VerticalSpeed, viewModel.BatteryVoltage,
            viewModel.BatteryRemaining, viewModel.GpsSatellites);
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            context.Custom(new HudDrawOperation(new Rect(Bounds.Size), snapshot));
        }
    }

    private sealed class HudDrawOperation(Rect bounds, HudSnapshot snapshot) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;
        public bool HitTest(Point point) => Bounds.Contains(point);

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;
            using var lease = feature.Lease();
            HudPainter.Draw(lease.SkCanvas, (float)Bounds.Width, (float)Bounds.Height, snapshot);
        }

        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }
    }
}

internal readonly record struct HudSnapshot(
    double Pitch, double Roll, double Heading,
    double AirSpeed, double GroundSpeed, double Altitude, double VerticalSpeed,
    double BatteryVoltage, double BatteryRemaining, int GpsSatellites);

internal static class HudPainter
{
    private const float PixelsPerDegree = 8f;

    public static void Draw(SKCanvas canvas, float width, float height, HudSnapshot hud)
    {
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, width, height));
        canvas.Clear(SKColors.Black);
        var centerX = width / 2f;
        var centerY = height / 2f;
        DrawAttitude(canvas, width, height, centerX, centerY, hud.Pitch, hud.Roll);
        DrawRollIndicator(canvas, centerX, centerY, hud.Roll);
        DrawAircraftSymbol(canvas, centerX, centerY);
        DrawHeading(canvas, centerX, hud.Heading);
        DrawReadouts(canvas, width, height, hud);
        canvas.Restore();
    }

    private static void DrawAttitude(SKCanvas canvas, float width, float height, float centerX, float centerY, double pitch, double roll)
    {
        canvas.Save();
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees((float)-roll);
        canvas.Translate(0, (float)(pitch * PixelsPerDegree));
        var size = Math.Max(width, height) * 2f;
        using var sky = Fill(new SKColor(0x4A, 0x90, 0xD9));
        using var ground = Fill(new SKColor(0x6B, 0x4A, 0x2A));
        using var horizon = Stroke(SKColors.White, 2);
        canvas.DrawRect(new SKRect(-size, -size, size, 0), sky);
        canvas.DrawRect(new SKRect(-size, 0, size, size), ground);
        canvas.DrawLine(-size, 0, size, 0, horizon);
        DrawPitchLadder(canvas);
        canvas.Restore();
    }

    private static void DrawPitchLadder(SKCanvas canvas)
    {
        using var line = Stroke(SKColors.White, 2);
        using var font = new SKFont(SKTypeface.Default, 14);
        using var text = Fill(SKColors.White);
        for (var degrees = -90; degrees <= 90; degrees += 10)
        {
            if (degrees == 0) continue;
            var y = -degrees * PixelsPerDegree;
            var length = degrees % 30 == 0 ? 60f : 30f;
            canvas.DrawLine(-length, y, length, y, line);
            if (degrees % 30 == 0)
                canvas.DrawText(Math.Abs(degrees).ToString(), length + 6, y + 5, SKTextAlign.Left, font, text);
        }
    }

    private static void DrawAircraftSymbol(SKCanvas canvas, float centerX, float centerY)
    {
        using var paint = Stroke(SKColors.Yellow, 4);
        paint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(centerX - 50, centerY, centerX - 15, centerY, paint);
        canvas.DrawLine(centerX + 15, centerY, centerX + 50, centerY, paint);
        canvas.DrawCircle(centerX, centerY, 4, paint);
    }

    private static void DrawRollIndicator(SKCanvas canvas, float centerX, float centerY, double roll)
    {
        var radius = Math.Min(centerX, centerY) * 0.9f;
        using var line = Stroke(SKColors.White, 2);
        foreach (var tick in new[] { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 })
        {
            canvas.Save();
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees(tick);
            canvas.DrawLine(0, -radius, 0, -radius + (tick == 0 ? 14 : 8), line);
            canvas.Restore();
        }

        canvas.Save();
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees((float)roll);
        using var pointer = new SKPath();
        pointer.MoveTo(0, -radius + 16);
        pointer.LineTo(-7, -radius + 28);
        pointer.LineTo(7, -radius + 28);
        pointer.Close();
        using var yellow = Fill(SKColors.Yellow);
        canvas.DrawPath(pointer, yellow);
        canvas.Restore();
    }

    private static void DrawHeading(SKCanvas canvas, float centerX, double heading)
    {
        using var background = Fill(new SKColor(0, 0, 0, 160));
        using var text = Fill(SKColors.White);
        using var font = new SKFont(SKTypeface.Default, 16);
        canvas.DrawRect(new SKRect(centerX - 60, 4, centerX + 60, 26), background);
        var normalized = ((int)Math.Round(heading) % 360 + 360) % 360;
        canvas.DrawText($"{normalized:000}°", centerX, 21, SKTextAlign.Center, font, text);
    }

    private static void DrawReadouts(SKCanvas canvas, float width, float height, HudSnapshot hud)
    {
        using var font = new SKFont(SKTypeface.Default, 14);
        using var text = Fill(SKColors.White);
        using var background = Fill(new SKColor(0, 0, 0, 150));
        DrawBox(canvas, 4, height / 2f - 38, 96, $"ASPD {hud.AirSpeed:0.0}", $"GSPD {hud.GroundSpeed:0.0}", font, text, background);
        DrawBox(canvas, width - 100, height / 2f - 38, 96, $"ALT {hud.Altitude:0.0}", $"VSI {hud.VerticalSpeed:0.0}", font, text, background);
        canvas.DrawRect(new SKRect(4, height - 25, 154, height - 4), background);
        canvas.DrawText($"BATT {hud.BatteryVoltage:0.0}V {hud.BatteryRemaining:0}%", 8, height - 9, SKTextAlign.Left, font, text);
        canvas.DrawRect(new SKRect(width - 84, height - 25, width - 4, height - 4), background);
        canvas.DrawText($"GPS {hud.GpsSatellites}", width - 80, height - 9, SKTextAlign.Left, font, text);
    }

    private static void DrawBox(SKCanvas canvas, float x, float y, float width, string first, string second, SKFont font, SKPaint text, SKPaint background)
    {
        canvas.DrawRect(new SKRect(x, y, x + width, y + 36), background);
        canvas.DrawText(first, x + 4, y + 15, SKTextAlign.Left, font, text);
        canvas.DrawText(second, x + 4, y + 31, SKTextAlign.Left, font, text);
    }

    private static SKPaint Fill(SKColor color) => new() { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
    private static SKPaint Stroke(SKColor color, float width) => new() { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width, IsAntialias = true };
}

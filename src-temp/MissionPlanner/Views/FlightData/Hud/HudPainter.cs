using SkiaSharp;

namespace MissionPlanner.Views.FlightData.Hud;

/// <summary>
/// Draws the attitude/heading HUD onto an <see cref="SKCanvas"/>.
/// Kept separate from <see cref="HudView"/> so the drawing logic can be reused
/// (e.g. for snapshots, other Skia hosts, or unit testing).
/// </summary>
public static class HudPainter
{
    private const float PixelsPerDegree = 8f;

    public static void Draw(SKCanvas canvas, SKImageInfo info, HudViewModel hud)
    {
        canvas.Clear(SKColors.Black);

        float width = info.Width;
        float height = info.Height;
        float centerX = width / 2f;
        float centerY = height / 2f;

        DrawAttitude(canvas, width, height, centerX, centerY, hud.Pitch, hud.Roll);
        DrawRollIndicator(canvas, centerX, centerY, height, hud.Roll);
        DrawFixedAircraftSymbol(canvas, centerX, centerY);
        DrawHeadingTape(canvas, width, centerX, hud.Heading);
        DrawTextReadouts(canvas, width, height, hud);
    }

    private static void DrawAttitude(SKCanvas canvas, float width, float height, float centerX, float centerY, double pitch, double roll)
    {
        canvas.Save();

        // Rotate/translate the horizon according to roll and pitch.
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees((float)-roll);
        canvas.Translate(0, (float)(pitch * PixelsPerDegree));

        // The horizon plane is drawn oversized so it still covers the view after rotation.
        float size = Math.Max(width, height) * 2f;

        using SKPaint skyPaint = new() { Color = new SKColor(0x4A, 0x90, 0xD9), Style = SKPaintStyle.Fill };
        using SKPaint groundPaint = new() { Color = new SKColor(0x6B, 0x4A, 0x2A), Style = SKPaintStyle.Fill };
        using SKPaint horizonLinePaint = new() { Color = SKColors.White, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };

        canvas.DrawRect(new SKRect(-size, -size, size, 0), skyPaint);
        canvas.DrawRect(new SKRect(-size, 0, size, size), groundPaint);
        canvas.DrawLine(-size, 0, size, 0, horizonLinePaint);

        DrawPitchLadder(canvas, size);

        canvas.Restore();
    }

    private static void DrawPitchLadder(SKCanvas canvas, float size)
    {
        using SKPaint linePaint = new() { Color = SKColors.White, StrokeWidth = 2, IsAntialias = true };
        using SKFont font = new(SKTypeface.Default, 14);
        using SKPaint textPaint = new() { Color = SKColors.White, IsAntialias = true };

        for (int degrees = -90; degrees <= 90; degrees += 10)
        {
            if (degrees == 0)
            {
                continue;
            }

            float y = -degrees * PixelsPerDegree;
            float lineLength = (degrees % 30 == 0) ? 60f : 30f;

            canvas.DrawLine(-lineLength, y, lineLength, y, linePaint);
            canvas.DrawLine(size - lineLength, y, size, y, linePaint);

            if (degrees % 30 == 0)
            {
                string label = Math.Abs(degrees).ToString();
                canvas.DrawText(label, lineLength + 6, y + 5, SKTextAlign.Left, font, textPaint);
            }
        }
    }

    private static void DrawFixedAircraftSymbol(SKCanvas canvas, float centerX, float centerY)
    {
        using SKPaint paint = new() { Color = SKColors.Yellow, StrokeWidth = 4, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };

        canvas.DrawLine(centerX - 50, centerY, centerX - 15, centerY, paint);
        canvas.DrawLine(centerX + 15, centerY, centerX + 50, centerY, paint);
        canvas.DrawCircle(centerX, centerY, 4, paint);
    }

    private static void DrawRollIndicator(SKCanvas canvas, float centerX, float centerY, float height, double roll)
    {
        float radius = Math.Min(centerX, centerY) * 0.9f;
        float arcTop = centerY - radius;

        using SKPaint arcPaint = new() { Color = SKColors.White, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };

        // Tick marks at -60,-45,-30,-20,-10,0,10,20,30,45,60 degrees.
        int[] ticks = [-60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60];
        foreach (int tick in ticks)
        {
            canvas.Save();
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees(tick);
            float length = tick == 0 ? 14f : 8f;
            canvas.DrawLine(0, -radius, 0, -radius + length, arcPaint);
            canvas.Restore();
        }

        // Roll pointer (fixed triangle pointing down at current roll position).
        canvas.Save();
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees((float)roll);

        using SKPaint pointerPaint = new() { Color = SKColors.Yellow, Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPath path = new();
        path.MoveTo(0, -radius + 16);
        path.LineTo(-7, -radius + 28);
        path.LineTo(7, -radius + 28);
        path.Close();
        canvas.DrawPath(path, pointerPaint);

        canvas.Restore();
    }

    private static void DrawHeadingTape(SKCanvas canvas, float width, float centerX, double heading)
    {
        float top = 4f;
        float boxHeight = 22f;

        using SKPaint background = new() { Color = new SKColor(0, 0, 0, 160), Style = SKPaintStyle.Fill };
        using SKFont font = new(SKTypeface.Default, 16);
        using SKPaint textPaint = new() { Color = SKColors.White, IsAntialias = true };
        using SKPaint pointerPaint = new() { Color = SKColors.Yellow, Style = SKPaintStyle.Fill };

        canvas.DrawRect(new SKRect(centerX - 60, top, centerX + 60, top + boxHeight), background);

        int normalizedHeading = ((int)Math.Round(heading) % 360 + 360) % 360;
        canvas.DrawText($"{normalizedHeading:000}°", centerX, top + 17, SKTextAlign.Center, font, textPaint);

        using SKPath pointer = new();
        pointer.MoveTo(centerX, top + boxHeight);
        pointer.LineTo(centerX - 6, top + boxHeight + 8);
        pointer.LineTo(centerX + 6, top + boxHeight + 8);
        pointer.Close();
        canvas.DrawPath(pointer, pointerPaint);
    }

    private static void DrawTextReadouts(SKCanvas canvas, float width, float height, HudViewModel hud)
    {
        using SKFont font = new(SKTypeface.Default, 16);
        using SKPaint textPaint = new() { Color = SKColors.White, IsAntialias = true };
        using SKPaint background = new() { Color = new SKColor(0, 0, 0, 140), Style = SKPaintStyle.Fill };

        // Left column: airspeed / ground speed.
        DrawLabeledBox(canvas, 4, height / 2f - 40, 90, 36, $"ASPD {hud.AirSpeed:0.0}", $"GSPD {hud.GroundSpeed:0.0}", font, textPaint, background);

        // Right column: altitude / vertical speed.
        DrawLabeledBox(canvas, width - 94, height / 2f - 40, 90, 36, $"ALT {hud.Altitude:0.0}", $"VSI {hud.VerticalSpeed:0.0}", font, textPaint, background);

        // Bottom-left: battery.
        canvas.DrawRect(new SKRect(4, height - 26, 140, height - 4), background);
        canvas.DrawText($"BATT {hud.BatteryVoltage:0.0}V {hud.BatteryRemaining:0}%", 8, height - 8, SKTextAlign.Left, font, textPaint);

        // Bottom-right: GPS.
        canvas.DrawRect(new SKRect(width - 90, height - 26, width - 4, height - 4), background);
        canvas.DrawText($"GPS {hud.GpsSatellites}", width - 86, height - 8, SKTextAlign.Left, font, textPaint);
    }

    private static void DrawLabeledBox(SKCanvas canvas, float x, float y, float w, float h, string line1, string line2, SKFont font, SKPaint textPaint, SKPaint background)
    {
        canvas.DrawRect(new SKRect(x, y, x + w, y + h), background);
        canvas.DrawText(line1, x + 4, y + 16, SKTextAlign.Left, font, textPaint);
        canvas.DrawText(line2, x + 4, y + 32, SKTextAlign.Left, font, textPaint);
    }
}

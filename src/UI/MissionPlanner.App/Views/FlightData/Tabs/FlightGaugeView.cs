using SkiaSharp;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Defines the visual scale used by a <see cref="FlightGaugeView"/>.</summary>
public enum FlightGaugeMode
{
    /// <summary>Displays a bounded value on a 270-degree instrument dial.</summary>
    Dial,

    /// <summary>Displays heading on a complete compass rose.</summary>
    Compass
}

/// <summary>Renders a responsive analog flight instrument using SkiaSharp.</summary>
public sealed class FlightGaugeView : Control
{
    /// <summary>Identifies the <see cref="Label"/> styled property.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FlightGaugeView, string>(nameof(Label), string.Empty);

    /// <summary>Identifies the <see cref="Value"/> styled property.</summary>
    public static readonly StyledProperty<double?> ValueProperty =
        AvaloniaProperty.Register<FlightGaugeView, double?>(nameof(Value));

    /// <summary>Identifies the <see cref="DisplayValue"/> styled property.</summary>
    public static readonly StyledProperty<string> DisplayValueProperty =
        AvaloniaProperty.Register<FlightGaugeView, string>(nameof(DisplayValue), "Unavailable");

    /// <summary>Identifies the <see cref="Unit"/> styled property.</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<FlightGaugeView, string>(nameof(Unit), string.Empty);

    /// <summary>Identifies the <see cref="Minimum"/> styled property.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<FlightGaugeView, double>(nameof(Minimum));

    /// <summary>Identifies the <see cref="Maximum"/> styled property.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<FlightGaugeView, double>(nameof(Maximum), 100d);

    /// <summary>Identifies the <see cref="Mode"/> styled property.</summary>
    public static readonly StyledProperty<FlightGaugeMode> ModeProperty =
        AvaloniaProperty.Register<FlightGaugeView, FlightGaugeMode>(nameof(Mode), FlightGaugeMode.Dial);

    /// <summary>Gets or sets the instrument label.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Gets or sets the numeric value used to position the needle.</summary>
    public double? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Gets or sets the formatted value displayed by the instrument.</summary>
    public string DisplayValue
    {
        get => GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    /// <summary>Gets or sets the unit displayed beneath the value.</summary>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Gets or sets the lower end of a dial scale.</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Gets or sets the upper end of a dial scale.</summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Gets or sets the visual scale used by the instrument.</summary>
    public FlightGaugeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    static FlightGaugeView()
    {
        AffectsRender<FlightGaugeView>(
            LabelProperty,
            ValueProperty,
            DisplayValueProperty,
            UnitProperty,
            MinimumProperty,
            MaximumProperty,
            ModeProperty);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var snapshot = new GaugeSnapshot(Label, Value, DisplayValue, Unit, Minimum, Maximum, Mode);
        context.Custom(new GaugeDrawOperation(new Rect(Bounds.Size), snapshot));
    }

    private static void Draw(SKCanvas canvas, float width, float height, GaugeSnapshot gauge)
    {
        using SKPaint background = new()
        {
            Color = new SKColor(17, 18, 20),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(0, 0, width, height, background);

        var center = new SKPoint(width / 2f, height / 2f);
        var radius = Math.Max(20f, (Math.Min(width, height) / 2f) - 12f);

        using SKPaint bezel = new()
        {
            Color = new SKColor(35, 37, 40),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        using SKPaint face = new()
        {
            Color = new SKColor(17, 18, 20),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(center, radius, bezel);
        canvas.DrawCircle(center, radius * 0.92f, face);

        if (gauge.Mode == FlightGaugeMode.Compass)
        {
            DrawCompass(canvas, center, radius);
        }
        else
        {
            DrawDial(canvas, center, radius, gauge.Minimum, gauge.Maximum);
        }

        DrawNeedle(canvas, center, radius, gauge);
        DrawReadout(canvas, center, radius, gauge);
    }

    private static void DrawDial(SKCanvas canvas, SKPoint center, float radius, double minimum, double maximum)
    {
        const int majorTicks = 10;
        const int minorTicksPerMajor = 5;
        var totalTicks = majorTicks * minorTicksPerMajor;
        using SKPaint tick = new()
        {
            Color = SKColors.White,
            StrokeWidth = 2,
            IsAntialias = true
        };
        using SKPaint majorTick = new()
        {
            Color = SKColors.White,
            StrokeWidth = 4,
            IsAntialias = true
        };
        using SKFont font = new(SKTypeface.Default, radius * 0.11f);
        using SKPaint text = new()
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        for (var index = 0; index <= totalTicks; index++)
        {
            var angle = -225f + (270f * index / totalTicks);
            var isMajor = index % minorTicksPerMajor == 0;
            DrawRadialLine(canvas, center, radius * (isMajor ? 0.76f : 0.82f), radius * 0.9f, angle, isMajor ? majorTick : tick);
            if (!isMajor)
            {
                continue;
            }

            var value = minimum + ((maximum - minimum) * index / totalTicks);
            var point = PointAt(center, radius * 0.64f, angle);
            canvas.DrawText(FormatScaleValue(value), point.X, point.Y + (radius * 0.04f), SKTextAlign.Center, font, text);
        }
    }

    private static void DrawCompass(SKCanvas canvas, SKPoint center, float radius)
    {
        using SKPaint tick = new()
        {
            Color = new SKColor(180, 180, 180),
            StrokeWidth = 2,
            IsAntialias = true
        };
        using SKPaint majorTick = new()
        {
            Color = SKColors.White,
            StrokeWidth = 4,
            IsAntialias = true
        };
        using SKFont font = new(SKTypeface.Default, radius * 0.13f);
        using SKPaint text = new()
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        string[] labels = ["N", "3", "6", "E", "12", "15", "S", "21", "24", "W", "30", "33"];

        for (var index = 0; index < 36; index++)
        {
            var angle = -90f + (index * 10f);
            var isMajor = index % 3 == 0;
            DrawRadialLine(canvas, center, radius * (isMajor ? 0.75f : 0.82f), radius * 0.9f, angle, isMajor ? majorTick : tick);
            if (isMajor)
            {
                var point = PointAt(center, radius * 0.62f, angle);
                canvas.DrawText(labels[index / 3], point.X, point.Y + (radius * 0.045f), SKTextAlign.Center, font, text);
            }
        }
    }

    private static void DrawNeedle(SKCanvas canvas, SKPoint center, float radius, GaugeSnapshot gauge)
    {
        if (gauge.Value is not { } value || !double.IsFinite(value))
        {
            return;
        }

        var angle = gauge.Mode == FlightGaugeMode.Compass
            ? -90f + (float)(((value % 360) + 360) % 360)
            : -225f + (270f * (float)Math.Clamp((value - gauge.Minimum) / Math.Max(double.Epsilon, gauge.Maximum - gauge.Minimum), 0, 1));
        var tip = PointAt(center, radius * 0.7f, angle);
        var tail = PointAt(center, radius * 0.14f, angle + 180f);
        using SKPaint shadow = new()
        {
            Color = new SKColor(0, 0, 0, 150),
            StrokeWidth = 7,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };
        using SKPaint needle = new()
        {
            Color = new SKColor(239, 68, 68),
            StrokeWidth = 4,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };
        using SKPaint hub = new()
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawLine(tail.X + 2, tail.Y + 2, tip.X + 2, tip.Y + 2, shadow);
        canvas.DrawLine(tail, tip, needle);
        canvas.DrawCircle(center, radius * 0.055f, hub);
    }

    private static void DrawReadout(SKCanvas canvas, SKPoint center, float radius, GaugeSnapshot gauge)
    {
        using SKFont labelFont = new(SKTypeface.Default, radius * 0.12f);
        using SKFont valueFont = new(SKTypeface.Default, radius * 0.14f);
        using SKPaint labelPaint = new()
        {
            Color = new SKColor(210, 210, 210),
            IsAntialias = true
        };
        using SKPaint valuePaint = new()
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        canvas.DrawText(gauge.Label, center.X, center.Y + (radius * 0.25f), SKTextAlign.Center, labelFont, labelPaint);
        canvas.DrawText($"{gauge.DisplayValue} {gauge.Unit}".Trim(), center.X, center.Y + (radius * 0.42f), SKTextAlign.Center, valueFont, valuePaint);
    }

    private static void DrawRadialLine(SKCanvas canvas, SKPoint center, float innerRadius, float outerRadius, float angle, SKPaint paint)
    {
        canvas.DrawLine(PointAt(center, innerRadius, angle), PointAt(center, outerRadius, angle), paint);
    }

    private static SKPoint PointAt(SKPoint center, float radius, float angle)
    {
        var radians = angle * MathF.PI / 180f;
        return new SKPoint(center.X + (MathF.Cos(radians) * radius), center.Y + (MathF.Sin(radians) * radius));
    }

    private static string FormatScaleValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.01 ? value.ToString("0") : value.ToString("0.#");
    }

    private sealed class GaugeDrawOperation(Rect bounds, GaugeSnapshot gauge) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;

        public bool HitTest(Point point) => Bounds.Contains(point);

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }

            using var lease = feature.Lease();
            Draw(lease.SkCanvas, (float)Bounds.Width, (float)Bounds.Height, gauge);
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }
    }

    private readonly record struct GaugeSnapshot(
        string Label,
        double? Value,
        string DisplayValue,
        string Unit,
        double Minimum,
        double Maximum,
        FlightGaugeMode Mode);
}

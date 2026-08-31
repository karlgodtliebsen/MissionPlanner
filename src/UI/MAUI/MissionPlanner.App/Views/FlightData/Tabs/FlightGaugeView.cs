using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

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
public sealed class FlightGaugeView : SKCanvasView
{
    /// <summary>Identifies the <see cref="Label"/> bindable property.</summary>
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(FlightGaugeView), string.Empty,
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Identifies the <see cref="Value"/> bindable property.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double?), typeof(FlightGaugeView), null,
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Identifies the <see cref="DisplayValue"/> bindable property.</summary>
    public static readonly BindableProperty DisplayValueProperty = BindableProperty.Create(
        nameof(DisplayValue), typeof(string), typeof(FlightGaugeView), "Unavailable",
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Identifies the <see cref="Unit"/> bindable property.</summary>
    public static readonly BindableProperty UnitProperty = BindableProperty.Create(
        nameof(Unit), typeof(string), typeof(FlightGaugeView), string.Empty,
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Identifies the <see cref="Minimum"/> bindable property.</summary>
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(
        nameof(Minimum), typeof(double), typeof(FlightGaugeView), 0d,
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Identifies the <see cref="Maximum"/> bindable property.</summary>
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(
        nameof(Maximum), typeof(double), typeof(FlightGaugeView), 100d,
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Identifies the <see cref="Mode"/> bindable property.</summary>
    public static readonly BindableProperty ModeProperty = BindableProperty.Create(
        nameof(Mode), typeof(FlightGaugeMode), typeof(FlightGaugeView), FlightGaugeMode.Dial,
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Gets or sets the instrument label.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Gets or sets the numeric value used to position the needle.</summary>
    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Gets or sets the formatted value displayed by the instrument.</summary>
    public string DisplayValue
    {
        get => (string)GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    /// <summary>Gets or sets the unit displayed beneath the value.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Gets or sets the lower end of a dial scale.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Gets or sets the upper end of a dial scale.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Gets or sets the visual scale used by the instrument.</summary>
    public FlightGaugeMode Mode
    {
        get => (FlightGaugeMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Initializes a new instance of the <see cref="FlightGaugeView"/> class.</summary>
    public FlightGaugeView()
    {
        PaintSurface += OnPaintSurface;
        EnableTouchEvents = false;
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((FlightGaugeView)bindable).InvalidateSurface();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        var canvas = args.Surface.Canvas;
        var width = args.Info.Width;
        var height = args.Info.Height;
        canvas.Clear(SKColors.Transparent);

        var center = new SKPoint(width / 2f, height / 2f);
        var radius = Math.Max(20f, Math.Min(width, height) / 2f - 12f);

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

        if (Mode == FlightGaugeMode.Compass)
        {
            DrawCompass(canvas, center, radius);
        }
        else
        {
            DrawDial(canvas, center, radius);
        }

        DrawNeedle(canvas, center, radius);
        DrawReadout(canvas, center, radius);
    }

    private void DrawDial(SKCanvas canvas, SKPoint center, float radius)
    {
        const int majorTicks = 10;
        const int minorTicksPerMajor = 5;
        var totalTicks = majorTicks * minorTicksPerMajor;
        using SKPaint tick = new() { Color = SKColors.White, StrokeWidth = 2, IsAntialias = true };
        using SKPaint majorTick = new() { Color = SKColors.White, StrokeWidth = 4, IsAntialias = true };
        using SKFont font = new(SKTypeface.Default, radius * 0.11f);
        using SKPaint text = new() { Color = SKColors.White, IsAntialias = true };

        for (var index = 0; index <= totalTicks; index++)
        {
            var angle = -225f + 270f * index / totalTicks;
            var isMajor = index % minorTicksPerMajor == 0;
            DrawRadialLine(canvas, center, radius * (isMajor ? 0.76f : 0.82f), radius * 0.9f, angle, isMajor ? majorTick : tick);
            if (!isMajor)
            {
                continue;
            }

            var value = Minimum + (Maximum - Minimum) * index / totalTicks;
            var point = PointAt(center, radius * 0.64f, angle);
            canvas.DrawText(FormatScaleValue(value), point.X, point.Y + radius * 0.04f, SKTextAlign.Center, font, text);
        }
    }

    private void DrawCompass(SKCanvas canvas, SKPoint center, float radius)
    {
        using SKPaint tick = new() { Color = new SKColor(180, 180, 180), StrokeWidth = 2, IsAntialias = true };
        using SKPaint majorTick = new() { Color = SKColors.White, StrokeWidth = 4, IsAntialias = true };
        using SKFont font = new(SKTypeface.Default, radius * 0.13f);
        using SKPaint text = new() { Color = SKColors.White, IsAntialias = true };
        string[] labels = ["N", "3", "6", "E", "12", "15", "S", "21", "24", "W", "30", "33"];

        for (var index = 0; index < 36; index++)
        {
            var angle = -90f + index * 10f;
            var isMajor = index % 3 == 0;
            DrawRadialLine(canvas, center, radius * (isMajor ? 0.75f : 0.82f), radius * 0.9f, angle, isMajor ? majorTick : tick);
            if (isMajor)
            {
                var point = PointAt(center, radius * 0.62f, angle);
                canvas.DrawText(labels[index / 3], point.X, point.Y + radius * 0.045f, SKTextAlign.Center, font, text);
            }
        }
    }

    private void DrawNeedle(SKCanvas canvas, SKPoint center, float radius)
    {
        if (Value is not { } value || !double.IsFinite(value))
        {
            return;
        }

        var angle = Mode == FlightGaugeMode.Compass
            ? -90f + (float)(((value % 360) + 360) % 360)
            : -225f + 270f * (float)Math.Clamp((value - Minimum) / Math.Max(double.Epsilon, Maximum - Minimum), 0, 1);
        var tip = PointAt(center, radius * 0.7f, angle);
        var tail = PointAt(center, radius * 0.14f, angle + 180f);
        using SKPaint shadow = new() { Color = new SKColor(0, 0, 0, 150), StrokeWidth = 7, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
        using SKPaint needle = new() { Color = new SKColor(239, 68, 68), StrokeWidth = 4, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
        using SKPaint hub = new() { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawLine(tail.X + 2, tail.Y + 2, tip.X + 2, tip.Y + 2, shadow);
        canvas.DrawLine(tail, tip, needle);
        canvas.DrawCircle(center, radius * 0.055f, hub);
    }

    private void DrawReadout(SKCanvas canvas, SKPoint center, float radius)
    {
        using SKFont labelFont = new(SKTypeface.Default, radius * 0.12f);
        using SKFont valueFont = new(SKTypeface.Default, radius * 0.14f);
        using SKPaint labelPaint = new() { Color = new SKColor(210, 210, 210), IsAntialias = true };
        using SKPaint valuePaint = new() { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(Label, center.X, center.Y + radius * 0.25f, SKTextAlign.Center, labelFont, labelPaint);
        canvas.DrawText($"{DisplayValue} {Unit}".Trim(), center.X, center.Y + radius * 0.42f, SKTextAlign.Center, valueFont, valuePaint);
    }

    private static void DrawRadialLine(SKCanvas canvas, SKPoint center, float innerRadius, float outerRadius, float angle, SKPaint paint) =>
        canvas.DrawLine(PointAt(center, innerRadius, angle), PointAt(center, outerRadius, angle), paint);

    private static SKPoint PointAt(SKPoint center, float radius, float angle)
    {
        var radians = angle * MathF.PI / 180f;
        return new SKPoint(center.X + MathF.Cos(radians) * radius, center.Y + MathF.Sin(radians) * radius);
    }

    private static string FormatScaleValue(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.01 ? value.ToString("0") : value.ToString("0.#");
}

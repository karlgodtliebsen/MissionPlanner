using Microsoft.Maui.Graphics;

namespace MissionPlanner.App.Views.Common;

/// <summary>Draws one live RC channel with configured and calibration markers.</summary>
public sealed class RadioChannelMeterView : GraphicsView
{
    private readonly MeterDrawable meterDrawable;

    /// <summary>Creates a radio channel meter.</summary>
    public RadioChannelMeterView()
    {
        HeightRequest = 32;
        MinimumHeightRequest = 24;
        meterDrawable = new MeterDrawable(this);
        Drawable = meterDrawable;
        UpdateSemanticDescription();
    }

    /// <summary>Gets or sets the one-based channel number used for accessibility.</summary>
    public int ChannelNumber { get => (int)GetValue(ChannelNumberProperty); set => SetValue(ChannelNumberProperty, value); }
    /// <summary>Identifies <see cref="ChannelNumber"/>.</summary>
    public static readonly BindableProperty ChannelNumberProperty = MeterProperty(nameof(ChannelNumber), 0);

    /// <summary>Gets or sets the resolved channel function used for accessibility.</summary>
    public string? FunctionName { get => (string?)GetValue(FunctionNameProperty); set => SetValue(FunctionNameProperty, value); }
    /// <summary>Identifies <see cref="FunctionName"/>.</summary>
    public static readonly BindableProperty FunctionNameProperty = MeterProperty<string?>(nameof(FunctionName), null);

    /// <summary>Gets or sets the latest raw PWM value.</summary>
    public int Pwm { get => (int)GetValue(PwmProperty); set => SetValue(PwmProperty, value); }
    /// <summary>Identifies <see cref="Pwm"/>.</summary>
    public static readonly BindableProperty PwmProperty = MeterProperty(nameof(Pwm), 1500);

    /// <summary>Gets or sets the lower visual-domain PWM value.</summary>
    public int DisplayMinimum { get => (int)GetValue(DisplayMinimumProperty); set => SetValue(DisplayMinimumProperty, value); }
    /// <summary>Identifies <see cref="DisplayMinimum"/>.</summary>
    public static readonly BindableProperty DisplayMinimumProperty = MeterProperty(nameof(DisplayMinimum), 800);

    /// <summary>Gets or sets the upper visual-domain PWM value.</summary>
    public int DisplayMaximum { get => (int)GetValue(DisplayMaximumProperty); set => SetValue(DisplayMaximumProperty, value); }
    /// <summary>Identifies <see cref="DisplayMaximum"/>.</summary>
    public static readonly BindableProperty DisplayMaximumProperty = MeterProperty(nameof(DisplayMaximum), 2200);

    /// <summary>Gets or sets the configured minimum endpoint.</summary>
    public int ConfiguredMinimum { get => (int)GetValue(ConfiguredMinimumProperty); set => SetValue(ConfiguredMinimumProperty, value); }
    /// <summary>Identifies <see cref="ConfiguredMinimum"/>.</summary>
    public static readonly BindableProperty ConfiguredMinimumProperty = MeterProperty(nameof(ConfiguredMinimum), 1100);

    /// <summary>Gets or sets the configured maximum endpoint.</summary>
    public int ConfiguredMaximum { get => (int)GetValue(ConfiguredMaximumProperty); set => SetValue(ConfiguredMaximumProperty, value); }
    /// <summary>Identifies <see cref="ConfiguredMaximum"/>.</summary>
    public static readonly BindableProperty ConfiguredMaximumProperty = MeterProperty(nameof(ConfiguredMaximum), 1900);

    /// <summary>Gets or sets the configured trim value.</summary>
    public int Trim { get => (int)GetValue(TrimProperty); set => SetValue(TrimProperty, value); }
    /// <summary>Identifies <see cref="Trim"/>.</summary>
    public static readonly BindableProperty TrimProperty = MeterProperty(nameof(Trim), 1500);

    /// <summary>Gets or sets the centered-axis dead zone in microseconds.</summary>
    public int DeadZone { get => (int)GetValue(DeadZoneProperty); set => SetValue(DeadZoneProperty, value); }
    /// <summary>Identifies <see cref="DeadZone"/>.</summary>
    public static readonly BindableProperty DeadZoneProperty = MeterProperty(nameof(DeadZone), 0);

    /// <summary>Gets or sets the minimum captured during the current calibration.</summary>
    public int? CapturedMinimum { get => (int?)GetValue(CapturedMinimumProperty); set => SetValue(CapturedMinimumProperty, value); }
    /// <summary>Identifies <see cref="CapturedMinimum"/>.</summary>
    public static readonly BindableProperty CapturedMinimumProperty = MeterProperty<int?>(nameof(CapturedMinimum), null);

    /// <summary>Gets or sets the maximum captured during the current calibration.</summary>
    public int? CapturedMaximum { get => (int?)GetValue(CapturedMaximumProperty); set => SetValue(CapturedMaximumProperty, value); }
    /// <summary>Identifies <see cref="CapturedMaximum"/>.</summary>
    public static readonly BindableProperty CapturedMaximumProperty = MeterProperty<int?>(nameof(CapturedMaximum), null);

    /// <summary>Gets or sets whether captured endpoint markers are displayed.</summary>
    public bool IsCapturing { get => (bool)GetValue(IsCapturingProperty); set => SetValue(IsCapturingProperty, value); }
    /// <summary>Identifies <see cref="IsCapturing"/>.</summary>
    public static readonly BindableProperty IsCapturingProperty = MeterProperty(nameof(IsCapturing), false);

    /// <summary>Gets or sets whether the retained input is stale.</summary>
    public bool IsStale { get => (bool)GetValue(IsStaleProperty); set => SetValue(IsStaleProperty, value); }
    /// <summary>Identifies <see cref="IsStale"/>.</summary>
    public static readonly BindableProperty IsStaleProperty = MeterProperty(nameof(IsStale), false);

    /// <summary>Gets or sets whether a live channel value is available.</summary>
    public bool HasSignal { get => (bool)GetValue(HasSignalProperty); set => SetValue(HasSignalProperty, value); }
    /// <summary>Identifies <see cref="HasSignal"/>.</summary>
    public static readonly BindableProperty HasSignalProperty = MeterProperty(nameof(HasSignal), true);

    /// <summary>Gets or sets the channel presentation semantics.</summary>
    public RadioChannelPresentationKind PresentationKind { get => (RadioChannelPresentationKind)GetValue(PresentationKindProperty); set => SetValue(PresentationKindProperty, value); }
    /// <summary>Identifies <see cref="PresentationKind"/>.</summary>
    public static readonly BindableProperty PresentationKindProperty = MeterProperty(nameof(PresentationKind), RadioChannelPresentationKind.Auxiliary);

    /// <summary>Gets or sets whether the configured channel direction is reversed.</summary>
    public bool IsReversed { get => (bool)GetValue(IsReversedProperty); set => SetValue(IsReversedProperty, value); }
    /// <summary>Identifies <see cref="IsReversed"/>.</summary>
    public static readonly BindableProperty IsReversedProperty = MeterProperty(nameof(IsReversed), false);

    private static BindableProperty MeterProperty<T>(string name, T defaultValue) => BindableProperty.Create(
        name,
        typeof(T),
        typeof(RadioChannelMeterView),
        defaultValue,
        propertyChanged: OnMeterPropertyChanged);

    private static void OnMeterPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var meter = (RadioChannelMeterView)bindable;
        meter.UpdateSemanticDescription();
        meter.Invalidate();
    }

    private void UpdateSemanticDescription()
    {
        var identity = FunctionName is null ? $"Channel {ChannelNumber}" : $"Channel {ChannelNumber}, {FunctionName}";
        var signal = !HasSignal ? "no signal" : IsStale ? $"stale at {Pwm} microseconds" : $"current {Pwm} microseconds";
        SemanticProperties.SetDescription(this, $"{identity}, {signal}, minimum {ConfiguredMinimum}, trim {Trim}, maximum {ConfiguredMaximum}{(IsReversed ? ", reversed" : string.Empty)}.");
    }

    private sealed class MeterDrawable(RadioChannelMeterView view) : IDrawable
    {
        private static readonly Color RailColor = Color.FromArgb("#596673");
        private static readonly Color RangeColor = Color.FromArgb("#547896");
        private static readonly Color DeadZoneColor = Color.FromArgb("#365E72");
        private static readonly Color ConfiguredColor = Color.FromArgb("#B8C4CE");
        private static readonly Color TrimColor = Color.FromArgb("#F0B44D");
        private static readonly Color CaptureColor = Color.FromArgb("#56C7A5");
        private static readonly Color CurrentColor = Color.FromArgb("#67B7E8");
        private static readonly Color StaleColor = Color.FromArgb("#9AA4AD");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            const float horizontalPadding = 8;
            var width = Math.Max(1, dirtyRect.Width - horizontalPadding * 2);
            var centerY = dirtyRect.Center.Y;
            var rail = new RectF(horizontalPadding, centerY - 3, width, 6);
            canvas.FillColor = RailColor;
            canvas.FillRoundedRectangle(rail, 3);

            var configuredLeft = Position(view.ConfiguredMinimum, horizontalPadding, width);
            var configuredRight = Position(view.ConfiguredMaximum, horizontalPadding, width);
            canvas.FillColor = RangeColor;
            canvas.FillRoundedRectangle(new RectF(configuredLeft, centerY - 3, Math.Max(1, configuredRight - configuredLeft), 6), 3);

            if (view.PresentationKind == RadioChannelPresentationKind.CenteredAxis && view.DeadZone > 0)
            {
                var deadZone = RadioChannelMeterGeometry.DeadZone(view.Trim, view.DeadZone, view.DisplayMinimum, view.DisplayMaximum, horizontalPadding, width);
                canvas.FillColor = DeadZoneColor;
                canvas.FillRectangle(deadZone.Left, centerY - 7, Math.Max(1, deadZone.Right - deadZone.Left), 14);
            }

            DrawTick(canvas, configuredLeft, centerY, 8, ConfiguredColor, 1.5f);
            DrawTick(canvas, configuredRight, centerY, 8, ConfiguredColor, 1.5f);

            if (view.PresentationKind == RadioChannelPresentationKind.CenteredAxis)
            {
                DrawTick(canvas, Position(1500, horizontalPadding, width), centerY, 5, ConfiguredColor.WithAlpha(0.45f), 1);
            }

            DrawTick(canvas, Position(view.Trim, horizontalPadding, width), centerY, 11, TrimColor, 2);

            if (view.IsCapturing)
            {
                if (view.CapturedMinimum is { } capturedMinimum)
                {
                    DrawTick(canvas, Position(capturedMinimum, horizontalPadding, width), centerY, 13, CaptureColor, 2.5f);
                }

                if (view.CapturedMaximum is { } capturedMaximum)
                {
                    DrawTick(canvas, Position(capturedMaximum, horizontalPadding, width), centerY, 13, CaptureColor, 2.5f);
                }
            }

            if (view.HasSignal)
            {
                canvas.FillColor = view.IsStale ? StaleColor : CurrentColor;
                canvas.FillCircle(Position(view.Pwm, horizontalPadding, width), centerY, view.IsStale ? 5 : 6);
                canvas.StrokeColor = Colors.White.WithAlpha(view.IsStale ? 0.45f : 0.9f);
                canvas.StrokeSize = 1;
                canvas.DrawCircle(Position(view.Pwm, horizontalPadding, width), centerY, view.IsStale ? 5 : 6);
            }
        }

        private float Position(int pwm, float left, float width) =>
            RadioChannelMeterGeometry.Position(pwm, view.DisplayMinimum, view.DisplayMaximum, left, width);

        private static void DrawTick(ICanvas canvas, float x, float centerY, float halfHeight, Color color, float strokeSize)
        {
            canvas.StrokeColor = color;
            canvas.StrokeSize = strokeSize;
            canvas.DrawLine(x, centerY - halfHeight, x, centerY + halfHeight);
        }
    }
}

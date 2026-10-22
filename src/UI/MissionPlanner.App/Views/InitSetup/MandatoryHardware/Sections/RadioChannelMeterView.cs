using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>
/// Draws one live RC channel with configured and calibration markers.
/// </summary>
public sealed class RadioChannelMeterView : Control
{
    /// <summary>Identifies the <see cref="Compact"/> styled property.</summary>
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(Compact));
    /// <summary>Gets or sets whether the meter uses the compact layout.</summary>
    public bool Compact { get => GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    /// <summary>Identifies the <see cref="ChannelNumber"/> styled property.</summary>
    public static readonly StyledProperty<int> ChannelNumberProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(ChannelNumber));
    /// <summary>Identifies the <see cref="FunctionName"/> styled property.</summary>
    public static readonly StyledProperty<string?> FunctionNameProperty = AvaloniaProperty.Register<RadioChannelMeterView, string?>(nameof(FunctionName));
    /// <summary>Identifies the <see cref="Pwm"/> styled property.</summary>
    public static readonly StyledProperty<int> PwmProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(Pwm), 1500);
    /// <summary>Identifies the <see cref="DisplayMinimum"/> styled property.</summary>
    public static readonly StyledProperty<int> DisplayMinimumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(DisplayMinimum), 800);
    /// <summary>Identifies the <see cref="DisplayMaximum"/> styled property.</summary>
    public static readonly StyledProperty<int> DisplayMaximumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(DisplayMaximum), 2200);
    /// <summary>Identifies the <see cref="ConfiguredMinimum"/> styled property.</summary>
    public static readonly StyledProperty<int> ConfiguredMinimumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(ConfiguredMinimum), 1100);
    /// <summary>Identifies the <see cref="ConfiguredMaximum"/> styled property.</summary>
    public static readonly StyledProperty<int> ConfiguredMaximumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(ConfiguredMaximum), 1900);
    /// <summary>Identifies the <see cref="Trim"/> styled property.</summary>
    public static readonly StyledProperty<int> TrimProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(Trim), 1500);
    /// <summary>Identifies the <see cref="DeadZone"/> styled property.</summary>
    public static readonly StyledProperty<int> DeadZoneProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(DeadZone));
    /// <summary>Identifies the <see cref="CapturedMinimum"/> styled property.</summary>
    public static readonly StyledProperty<int?> CapturedMinimumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int?>(nameof(CapturedMinimum));
    /// <summary>Identifies the <see cref="CapturedMaximum"/> styled property.</summary>
    public static readonly StyledProperty<int?> CapturedMaximumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int?>(nameof(CapturedMaximum));
    /// <summary>Identifies the <see cref="CandidateTrim"/> styled property.</summary>
    public static readonly StyledProperty<int?> CandidateTrimProperty = AvaloniaProperty.Register<RadioChannelMeterView, int?>(nameof(CandidateTrim));
    /// <summary>Identifies the <see cref="IsCapturing"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsCapturingProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(IsCapturing));
    /// <summary>Identifies the <see cref="IsStale"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsStaleProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(IsStale));
    /// <summary>Identifies the <see cref="HasSignal"/> styled property.</summary>
    public static readonly StyledProperty<bool> HasSignalProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(HasSignal), true);
    /// <summary>Identifies the <see cref="PresentationKind"/> styled property.</summary>
    public static readonly StyledProperty<RadioChannelPresentationKind> PresentationKindProperty = AvaloniaProperty.Register<RadioChannelMeterView, RadioChannelPresentationKind>(nameof(PresentationKind));
    /// <summary>Identifies the <see cref="IsReversed"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsReversedProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(IsReversed));

    static RadioChannelMeterView()
    {
        AffectsRender<RadioChannelMeterView>(
        CompactProperty, ChannelNumberProperty, FunctionNameProperty, PwmProperty, DisplayMinimumProperty, DisplayMaximumProperty, ConfiguredMinimumProperty,
        ConfiguredMaximumProperty, TrimProperty, DeadZoneProperty, CapturedMinimumProperty,
        CapturedMaximumProperty, CandidateTrimProperty, IsCapturingProperty, IsStaleProperty,
        HasSignalProperty, PresentationKindProperty, IsReversedProperty);
    }

    /// <summary>Creates the meter.</summary>
    public RadioChannelMeterView()
    {
        Height = 32;
        MinHeight = 24;
    }

    /// <summary>Gets or sets the RC channel number shown on the meter.</summary>
    public int ChannelNumber
    {
        get => GetValue(ChannelNumberProperty); set => SetValue(ChannelNumberProperty, value);
    }
    /// <summary>Gets or sets the name of the function assigned to the channel.</summary>
    public string? FunctionName
    {
        get => GetValue(FunctionNameProperty); set => SetValue(FunctionNameProperty, value);
    }
    /// <summary>Gets or sets the live PWM value in microseconds.</summary>
    public int Pwm
    {
        get => GetValue(PwmProperty); set => SetValue(PwmProperty, value);
    }
    /// <summary>Gets or sets the lowest PWM value on the meter scale.</summary>
    public int DisplayMinimum
    {
        get => GetValue(DisplayMinimumProperty); set => SetValue(DisplayMinimumProperty, value);
    }
    /// <summary>Gets or sets the highest PWM value on the meter scale.</summary>
    public int DisplayMaximum
    {
        get => GetValue(DisplayMaximumProperty); set => SetValue(DisplayMaximumProperty, value);
    }
    /// <summary>Gets or sets the configured minimum PWM value.</summary>
    public int ConfiguredMinimum
    {
        get => GetValue(ConfiguredMinimumProperty); set => SetValue(ConfiguredMinimumProperty, value);
    }
    /// <summary>Gets or sets the configured maximum PWM value.</summary>
    public int ConfiguredMaximum
    {
        get => GetValue(ConfiguredMaximumProperty); set => SetValue(ConfiguredMaximumProperty, value);
    }
    /// <summary>Gets or sets the configured trim PWM value.</summary>
    public int Trim
    {
        get => GetValue(TrimProperty); set => SetValue(TrimProperty, value);
    }
    /// <summary>Gets or sets the configured dead zone in PWM microseconds.</summary>
    public int DeadZone
    {
        get => GetValue(DeadZoneProperty); set => SetValue(DeadZoneProperty, value);
    }
    /// <summary>Gets or sets the lowest PWM value captured during calibration.</summary>
    public int? CapturedMinimum
    {
        get => GetValue(CapturedMinimumProperty); set => SetValue(CapturedMinimumProperty, value);
    }
    /// <summary>Gets or sets the highest PWM value captured during calibration.</summary>
    public int? CapturedMaximum
    {
        get => GetValue(CapturedMaximumProperty); set => SetValue(CapturedMaximumProperty, value);
    }
    /// <summary>Gets or sets the trim candidate captured during calibration.</summary>
    public int? CandidateTrim
    {
        get => GetValue(CandidateTrimProperty); set => SetValue(CandidateTrimProperty, value);
    }
    /// <summary>Gets or sets whether a calibration capture is running.</summary>
    public bool IsCapturing
    {
        get => GetValue(IsCapturingProperty); set => SetValue(IsCapturingProperty, value);
    }
    /// <summary>Gets or sets whether the PWM reading is stale.</summary>
    public bool IsStale
    {
        get => GetValue(IsStaleProperty); set => SetValue(IsStaleProperty, value);
    }
    /// <summary>Gets or sets whether the receiver signal is present.</summary>
    public bool HasSignal
    {
        get => GetValue(HasSignalProperty); set => SetValue(HasSignalProperty, value);
    }
    /// <summary>Gets or sets how the channel is presented, for example as a stick or a switch.</summary>
    public RadioChannelPresentationKind PresentationKind
    {
        get => GetValue(PresentationKindProperty); set => SetValue(PresentationKindProperty, value);
    }
    /// <summary>Gets or sets whether the channel is reversed.</summary>
    public bool IsReversed
    {
        get => GetValue(IsReversedProperty); set => SetValue(IsReversedProperty, value);
    }

    /// <summary>Draws the meter, its configured range and the calibration markers.</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Compact)
        {
            var barWidth = Math.Max(0, Bounds.Width);
            var barHeight = Math.Min(20, Bounds.Height);
            var top = (Bounds.Height - barHeight) / 2;
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#45484D")), null,
                new Rect(0, top, barWidth, barHeight), barHeight / 2, barHeight / 2);
            if (HasSignal && barWidth > 0)
            {
                var color = IsStale ? "#7A818A" : RadioChannelColors.For(FunctionName, ChannelNumber);
                context.DrawRectangle(new SolidColorBrush(Color.Parse(color)), null,
                    new Rect(0, top, Position(Pwm, 0, barWidth), barHeight), barHeight / 2, barHeight / 2);
            }
            return;
        }
        const double padding = 8;
        var width = Math.Max(1, Bounds.Width - (padding * 2));
        var center = Bounds.Height / 2;
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#596673")), null, new Rect(padding, center - 3, width, 6), 3, 3);
        var left = Position(ConfiguredMinimum, padding, width);
        var right = Position(ConfiguredMaximum, padding, width);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#547896")), null, new Rect(left, center - 3, Math.Max(1, right - left), 6), 3, 3);
        DrawTick(context, left, center, 8, "#B8C4CE", 1.5);
        DrawTick(context, right, center, 8, "#B8C4CE", 1.5);
        DrawTick(context, Position(Trim, padding, width), center, 11, "#F0B44D", 2);
        if (IsCapturing && CapturedMinimum is { } min)
        {
            DrawTick(context, Position(min, padding, width), center, 13, "#56C7A5", 2.5);
        }

        if (IsCapturing && CapturedMaximum is { } max)
        {
            DrawTick(context, Position(max, padding, width), center, 13, "#56C7A5", 2.5);
        }

        if (HasSignal)
        {
            var brush = new SolidColorBrush(Color.Parse(IsStale ? "#9AA4AD" : "#67B7E8"));
            context.DrawEllipse(brush, null, new Point(Position(Pwm, padding, width), center), IsStale ? 5 : 6, IsStale ? 5 : 6);
        }
    }

    private double Position(int pwm, double left, double width)
    {
        return RadioChannelMeterGeometry.Position(pwm, DisplayMinimum, DisplayMaximum, (float)left, (float)width);
    }

    private static void DrawTick(DrawingContext context, double x, double center, double halfHeight, string color, double thickness)
    {
        context.DrawLine(new Pen(new SolidColorBrush(Color.Parse(color)), thickness), new Point(x, center - halfHeight), new Point(x, center + halfHeight));
    }
}

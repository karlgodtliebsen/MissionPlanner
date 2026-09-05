using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;

/// <summary>Draws one live RC channel with configured and calibration markers.</summary>
public sealed class RadioChannelMeterView : Control
{
    public static readonly StyledProperty<int> ChannelNumberProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(ChannelNumber));
    public static readonly StyledProperty<string?> FunctionNameProperty = AvaloniaProperty.Register<RadioChannelMeterView, string?>(nameof(FunctionName));
    public static readonly StyledProperty<int> PwmProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(Pwm), 1500);
    public static readonly StyledProperty<int> DisplayMinimumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(DisplayMinimum), 800);
    public static readonly StyledProperty<int> DisplayMaximumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(DisplayMaximum), 2200);
    public static readonly StyledProperty<int> ConfiguredMinimumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(ConfiguredMinimum), 1100);
    public static readonly StyledProperty<int> ConfiguredMaximumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(ConfiguredMaximum), 1900);
    public static readonly StyledProperty<int> TrimProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(Trim), 1500);
    public static readonly StyledProperty<int> DeadZoneProperty = AvaloniaProperty.Register<RadioChannelMeterView, int>(nameof(DeadZone));
    public static readonly StyledProperty<int?> CapturedMinimumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int?>(nameof(CapturedMinimum));
    public static readonly StyledProperty<int?> CapturedMaximumProperty = AvaloniaProperty.Register<RadioChannelMeterView, int?>(nameof(CapturedMaximum));
    public static readonly StyledProperty<int?> CandidateTrimProperty = AvaloniaProperty.Register<RadioChannelMeterView, int?>(nameof(CandidateTrim));
    public static readonly StyledProperty<bool> IsCapturingProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(IsCapturing));
    public static readonly StyledProperty<bool> IsStaleProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(IsStale));
    public static readonly StyledProperty<bool> HasSignalProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(HasSignal), true);
    public static readonly StyledProperty<RadioChannelPresentationKind> PresentationKindProperty = AvaloniaProperty.Register<RadioChannelMeterView, RadioChannelPresentationKind>(nameof(PresentationKind));
    public static readonly StyledProperty<bool> IsReversedProperty = AvaloniaProperty.Register<RadioChannelMeterView, bool>(nameof(IsReversed));

    static RadioChannelMeterView() => AffectsRender<RadioChannelMeterView>(
        PwmProperty, DisplayMinimumProperty, DisplayMaximumProperty, ConfiguredMinimumProperty,
        ConfiguredMaximumProperty, TrimProperty, DeadZoneProperty, CapturedMinimumProperty,
        CapturedMaximumProperty, CandidateTrimProperty, IsCapturingProperty, IsStaleProperty,
        HasSignalProperty, PresentationKindProperty, IsReversedProperty);

    public RadioChannelMeterView() { Height = 32; MinHeight = 24; }

    public int ChannelNumber { get => GetValue(ChannelNumberProperty); set => SetValue(ChannelNumberProperty, value); }
    public string? FunctionName { get => GetValue(FunctionNameProperty); set => SetValue(FunctionNameProperty, value); }
    public int Pwm { get => GetValue(PwmProperty); set => SetValue(PwmProperty, value); }
    public int DisplayMinimum { get => GetValue(DisplayMinimumProperty); set => SetValue(DisplayMinimumProperty, value); }
    public int DisplayMaximum { get => GetValue(DisplayMaximumProperty); set => SetValue(DisplayMaximumProperty, value); }
    public int ConfiguredMinimum { get => GetValue(ConfiguredMinimumProperty); set => SetValue(ConfiguredMinimumProperty, value); }
    public int ConfiguredMaximum { get => GetValue(ConfiguredMaximumProperty); set => SetValue(ConfiguredMaximumProperty, value); }
    public int Trim { get => GetValue(TrimProperty); set => SetValue(TrimProperty, value); }
    public int DeadZone { get => GetValue(DeadZoneProperty); set => SetValue(DeadZoneProperty, value); }
    public int? CapturedMinimum { get => GetValue(CapturedMinimumProperty); set => SetValue(CapturedMinimumProperty, value); }
    public int? CapturedMaximum { get => GetValue(CapturedMaximumProperty); set => SetValue(CapturedMaximumProperty, value); }
    public int? CandidateTrim { get => GetValue(CandidateTrimProperty); set => SetValue(CandidateTrimProperty, value); }
    public bool IsCapturing { get => GetValue(IsCapturingProperty); set => SetValue(IsCapturingProperty, value); }
    public bool IsStale { get => GetValue(IsStaleProperty); set => SetValue(IsStaleProperty, value); }
    public bool HasSignal { get => GetValue(HasSignalProperty); set => SetValue(HasSignalProperty, value); }
    public RadioChannelPresentationKind PresentationKind { get => GetValue(PresentationKindProperty); set => SetValue(PresentationKindProperty, value); }
    public bool IsReversed { get => GetValue(IsReversedProperty); set => SetValue(IsReversedProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        const double padding = 8;
        var width = Math.Max(1, Bounds.Width - padding * 2);
        var center = Bounds.Height / 2;
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#596673")), null, new Rect(padding, center - 3, width, 6), 3, 3);
        var left = Position(ConfiguredMinimum, padding, width);
        var right = Position(ConfiguredMaximum, padding, width);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#547896")), null, new Rect(left, center - 3, Math.Max(1, right - left), 6), 3, 3);
        DrawTick(context, left, center, 8, "#B8C4CE", 1.5);
        DrawTick(context, right, center, 8, "#B8C4CE", 1.5);
        DrawTick(context, Position(Trim, padding, width), center, 11, "#F0B44D", 2);
        if (IsCapturing && CapturedMinimum is { } min) DrawTick(context, Position(min, padding, width), center, 13, "#56C7A5", 2.5);
        if (IsCapturing && CapturedMaximum is { } max) DrawTick(context, Position(max, padding, width), center, 13, "#56C7A5", 2.5);
        if (HasSignal)
        {
            var brush = new SolidColorBrush(Color.Parse(IsStale ? "#9AA4AD" : "#67B7E8"));
            context.DrawEllipse(brush, null, new Point(Position(Pwm, padding, width), center), IsStale ? 5 : 6, IsStale ? 5 : 6);
        }
    }

    private double Position(int pwm, double left, double width) =>
        RadioChannelMeterGeometry.Position(pwm, DisplayMinimum, DisplayMaximum, (float)left, (float)width);

    private static void DrawTick(DrawingContext context, double x, double center, double halfHeight, string color, double thickness) =>
        context.DrawLine(new Pen(new SolidColorBrush(Color.Parse(color)), thickness), new Point(x, center - halfHeight), new Point(x, center + halfHeight));
}

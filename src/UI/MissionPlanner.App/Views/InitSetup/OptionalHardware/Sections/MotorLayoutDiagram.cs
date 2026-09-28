using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>A catalogue motor and its independently resolved physical outputs.</summary>
public sealed record MotorDiagramPoint(MotorLayoutMotor Motor, MotorOutputResolution? Output)
{
    /// <summary>Top-view rightward position derived from the frame roll factor.</summary>
    public double X => -Motor.Roll;
    /// <summary>Top-view downward position derived from the frame pitch factor.</summary>
    public double Y => -Motor.Pitch;
    /// <summary>Expected logical mapping, never a claim about observed wiring.</summary>
    public string Label => $"{(char)('A' + Motor.TestOrder - 1)} → Motor {Motor.MotorNumber} · {Motor.Rotation}";
    /// <summary>Explicit output assignment or missing/ambiguous state.</summary>
    public string OutputLabel => Output is null ? "SERVO assignment unknown" :
        $"SERVO: {(Output.OutputChannels.Count == 0 ? "none reported" : string.Join(", ", Output.OutputChannels))} ({Output.Status})";
}

/// <summary>Read-only top view of expected frame geometry; never initiates motor tests.</summary>
public sealed class MotorLayoutDiagram : Control
{
    /// <summary>Motor diagram data property.</summary>
    public static readonly StyledProperty<IReadOnlyList<MotorDiagramPoint>> MotorsProperty =
        AvaloniaProperty.Register<MotorLayoutDiagram, IReadOnlyList<MotorDiagramPoint>>(nameof(Motors), []);

    /// <summary>Gets or sets expected motors and their output assignments.</summary>
    public IReadOnlyList<MotorDiagramPoint> Motors
    {
        get => GetValue(MotorsProperty);
        set => SetValue(MotorsProperty, value);
    }

    static MotorLayoutDiagram() => AffectsRender<MotorLayoutDiagram>(MotorsProperty);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        void Text(string value, double x, double y, double size = 12)
        {
            var text = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, size, Brushes.White);
            context.DrawText(text, new Point(x - text.Width / 2, y));
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        Text("TOP VIEW · ↑ NOSE / FRONT", center.X, 4, 14);
        if (Motors.Count == 0)
        {
            Text("Layout unavailable for the reported frame", center.X, center.Y);
            return;
        }
        var scale = Math.Max(0.1, Motors.Max(motor => Math.Max(Math.Abs(motor.X), Math.Abs(motor.Y))));
        foreach (var motor in Motors)
        {
            var point = new Point(center.X + motor.X / scale * Bounds.Width * 0.27,
                center.Y + motor.Y / scale * Bounds.Height * 0.28);
            context.DrawLine(new Pen(Brushes.Gray, 2), center, point);
            context.DrawEllipse(Brushes.DimGray, new Pen(Brushes.LightBlue, 2), point, 16, 16);
            Text(((char)('A' + motor.Motor.TestOrder - 1)).ToString(), point.X, point.Y - 9, 16);
            Text(motor.Label, point.X, point.Y + 19);
            Text(motor.OutputLabel, point.X, point.Y + 35, 11);
        }
        Text("Expected layout · Verify physical wiring separately", center.X, Bounds.Height - 18, 11);
    }
}

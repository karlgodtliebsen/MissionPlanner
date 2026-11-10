using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Current controller mapping plus an explicitly staged physical observation.</summary>
public sealed record MotorDiagramPoint(MotorLayoutMotor Motor, MotorOutputResolution? Output,
    int? PendingOutputChannel = null, bool IsChanged = false, bool IsActive = false, bool IsIdentifying = false)
{
    public double X => -Motor.Roll;
    public double Y => -Motor.Pitch;
    public string Label => $"{(char)('A' + Motor.TestOrder - 1)} → Motor {Motor.MotorNumber} · {Motor.Rotation}";
    private string CurrentOutputs => Output is null ? "unknown" : Output.OutputChannels.Count == 0 ? "none" : string.Join(", ", Output.OutputChannels);
    public string OutputLabel => IsIdentifying
        ? PendingOutputChannel is { } observed ? $"SERVO: {observed} · Identified (not saved)" : "Not identified · select when pulsing"
        : PendingOutputChannel is { } channel ? $"SERVO: {CurrentOutputs} → {channel} (not saved)" : Output is null ? "SERVO assignment unknown" :
        $"SERVO: {CurrentOutputs} ({Output.Status})";
}

/// <summary>Shared frame diagram for manual testing and motor-order identification.</summary>
public sealed class MotorLayoutDiagram : Panel
{
    public static readonly StyledProperty<IReadOnlyList<MotorDiagramPoint>> MotorsProperty =
        AvaloniaProperty.Register<MotorLayoutDiagram, IReadOnlyList<MotorDiagramPoint>>(nameof(Motors), []);
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<MotorLayoutDiagram, ICommand?>(nameof(SelectCommand));
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<MotorLayoutDiagram, bool>(nameof(IsInteractive));

    public IReadOnlyList<MotorDiagramPoint> Motors { get => GetValue(MotorsProperty); set => SetValue(MotorsProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
    public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MotorsProperty || change.Property == SelectCommandProperty || change.Property == IsInteractiveProperty)
        {
            // Keep the same buttons during live refreshes, including between pointer down/up.
            // Recreating them loses pointer capture and silently drops the user's selection.
            if (!Children.OfType<Button>().Select(button => ((MotorLayoutMotor)button.CommandParameter!).MotorNumber)
                .SequenceEqual(Motors.Select(point => point.Motor.MotorNumber)))
            {
                Children.Clear();
                Children.Add(new FrameGeometry(this) { IsHitTestVisible = false });
                foreach (var motor in Motors)
                {
                    var button = new Button
                    {
                        Width = 54, Height = 54, CornerRadius = new CornerRadius(27), Padding = new Thickness(8),
                        CommandParameter = motor.Motor, Foreground = Brushes.White,
                        Content = new MaterialIcon { Width = 30, Height = 30 },
                        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    button.Classes.Add("EmbeddedIconButton");
                    Children.Add(button);
                }
            }
            foreach (var (button, motor) in Children.OfType<Button>().Zip(Motors))
            {
                button.Command = IsInteractive ? SelectCommand : null;
                button.CommandParameter = motor.Motor;
                button.IsHitTestVisible = button.Focusable = IsInteractive;
                button.Background = motor.IsChanged ? Brushes.DarkOrange : Brushes.DimGray;
                button.BorderBrush = motor.IsActive || motor.IsChanged ? Brushes.DarkOrange : Brushes.LightBlue;
                button.BorderThickness = new Thickness(motor.IsActive ? 4 : 2);
                ((MaterialIcon)button.Content!).Kind = motor.PendingOutputChannel.HasValue ? MaterialIconKind.Check : MaterialIconKind.Fan;
                AutomationProperties.SetName(button, $"Select motor at {(motor.Motor.Pitch > 0 ? "front" : motor.Motor.Pitch < 0 ? "rear" : "centre")}, {(motor.Motor.Roll < 0 ? "right" : motor.Motor.Roll > 0 ? "left" : "centre")}, position {motor.Motor.TestOrder}");
                ToolTip.SetTip(button, IsInteractive ? motor.IsIdentifying ? "Select the motor you see pulsing" : "Test this motor" : motor.OutputLabel);
            }
            if (Children.Count > 0) Children[0].InvalidateVisual();
            InvalidateMeasure();
        }
    }

    private Point Position(MotorDiagramPoint motor, Size size)
    {
        var scale = Math.Max(0.1, Motors.Max(item => Math.Max(Math.Abs(item.X), Math.Abs(item.Y))));
        return new(size.Width / 2 + motor.X / scale * Math.Max(0, size.Width / 2 - 150),
            size.Height / 2 - 15 + motor.Y / scale * Math.Max(0, size.Height / 2 - 80));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(new Size(54, 54));
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 700,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 330);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count > 0) Children[0].Arrange(new Rect(finalSize));
        for (var i = 1; i < Children.Count; i++)
        {
            var point = Position(Motors[i - 1], finalSize);
            Children[i].Arrange(new Rect(point.X - 27, point.Y - 27, 54, 54));
        }
        return finalSize;
    }

    private void DrawFrame(DrawingContext context)
    {
        void Text(string value, double x, double y, IBrush? color = null)
        {
            var text = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, 12, color ?? Brushes.White);
            context.DrawText(text, new Point(x - text.Width / 2, y));
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2 - 15);
        Text("TOP VIEW · ↑ NOSE / FRONT", center.X, 2);
        if (Motors.Count == 0)
        {
            Text("Frame layout unavailable", center.X, center.Y);
            return;
        }
        foreach (var motor in Motors)
        {
            var point = Position(motor, Bounds.Size);
            context.DrawLine(new Pen(motor.IsActive ? Brushes.DarkOrange : Brushes.Gray, 5), center, point);
            Text(motor.Label, point.X, point.Y + 31);
            Text(motor.OutputLabel, point.X, point.Y + 48, motor.IsChanged ? Brushes.DarkOrange : Brushes.White);
        }
        context.DrawEllipse(Brushes.DimGray, new Pen(Brushes.LightGray, 2), center, 19, 26);
        var tip = new Point(center.X, center.Y - 17);
        context.DrawLine(new Pen(Brushes.White, 3), new Point(center.X, center.Y + 12), tip);
        context.DrawLine(new Pen(Brushes.White, 3), tip, new Point(center.X - 8, center.Y - 7));
        context.DrawLine(new Pen(Brushes.White, 3), tip, new Point(center.X + 8, center.Y - 7));
    }

    private sealed class FrameGeometry(MotorLayoutDiagram owner) : Control
    {
        public override void Render(DrawingContext context) => owner.DrawFrame(context);
    }
}

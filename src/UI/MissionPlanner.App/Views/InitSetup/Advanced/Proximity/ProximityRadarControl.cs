using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlanner.Core.Setup.Advanced.Proximity;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Proximity;

/// <summary>Draws a bounded snapshot in one retained control; forward is up and bearings increase clockwise.</summary>
public sealed class ProximityRadarControl : Control
{
    /// <summary>Identifies the coalesced snapshot.</summary>
    public static readonly StyledProperty<ProximitySnapshot> SnapshotProperty =
        AvaloniaProperty.Register<ProximityRadarControl, ProximitySnapshot>(nameof(Snapshot), ProximitySnapshot.Empty);
    /// <summary>Identifies the displayed radius in meters.</summary>
    public static readonly StyledProperty<double> RangeMetersProperty =
        AvaloniaProperty.Register<ProximityRadarControl, double>(nameof(RangeMeters), 20);
    /// <summary>Gets or sets the displayed snapshot.</summary>
    public ProximitySnapshot Snapshot { get => GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    /// <summary>Gets or sets the displayed radius in meters.</summary>
    public double RangeMeters { get => GetValue(RangeMetersProperty); set => SetValue(RangeMetersProperty, value); }

    static ProximityRadarControl() => AffectsRender<ProximityRadarControl>(SnapshotProperty, RangeMetersProperty);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 40;
        if (radius <= 0)
        {
            return;
        }
        var range = double.IsFinite(RangeMeters) ? Math.Clamp(RangeMeters, 0.1, 1000) : 20;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var grid = new Pen(Brushes.Gray);
        for (var ring = 1; ring <= 4; ring++)
        {
            var size = radius * ring / 4;
            context.DrawEllipse(null, grid, center, size, size);
            Label(context, $"{range * ring / 4:G3} m", center + new Vector(4, -size));
        }
        context.DrawLine(grid, center - new Vector(radius, 0), center + new Vector(radius, 0));
        context.DrawLine(grid, center - new Vector(0, radius), center + new Vector(0, radius));
        Label(context, "Forward 0°", center - new Vector(32, radius + 24));
        Label(context, "180°", center + new Vector(-14, radius + 8));
        Label(context, "270°", center - new Vector(radius + 36, 0));
        Label(context, "90°", center + new Vector(radius + 4, 0));
        foreach (var point in Snapshot.Points)
        {
            if (point.State is not (ProximitySampleState.Valid or ProximitySampleState.TooClose)
                || point.BearingDegrees is not { } bearing || point.DistanceMeters is not { } distance
                || distance > range)
            {
                continue;
            }
            var angle = bearing * Math.PI / 180;
            var position = center + new Vector(Math.Sin(angle), -Math.Cos(angle)) * (distance / range * radius);
            var brush = point.State == ProximitySampleState.TooClose ? Brushes.OrangeRed : Brushes.DeepSkyBlue;
            context.DrawEllipse(brush, null, position, 4, 4);
            if (point == Snapshot.Nearest)
            {
                context.DrawEllipse(null, new Pen(Brushes.White, 2), position, 7, 7);
            }
        }
        context.DrawEllipse(Brushes.White, null, center, 3, 3);
    }

    private static void Label(DrawingContext context, string text, Point origin) => context.DrawText(
        new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), 12, Brushes.White), origin);
}

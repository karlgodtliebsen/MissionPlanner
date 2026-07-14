using System.ComponentModel;
using MissionPlanner.App.Configuration;
using SkiaSharp.Views.Maui;

namespace MissionPlanner.App.Views.FlightData.Hud;

/// <summary>
/// Represents the HUD (Head-Up Display) view.
/// </summary>
public partial class HudView : ContentView
{
    private readonly HudViewModel viewModel;


    /// <summary>
    /// Initializes a new instance of the <see cref="HudView"/> class.
    /// </summary>
    public HudView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<HudViewModel>();
        BindingContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }


    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Canvas.InvalidateSurface();
    }

    private void Canvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        HudPainter.Draw(e.Surface.Canvas, e.Info, viewModel);
    }
}

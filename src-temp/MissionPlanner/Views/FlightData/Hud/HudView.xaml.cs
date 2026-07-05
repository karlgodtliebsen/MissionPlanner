using System.ComponentModel;

using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MissionPlanner.Views.FlightData.Hud;

public partial class HudView : ContentView
{
    private readonly HudViewModel viewModel;

    public HudView(HudViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

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

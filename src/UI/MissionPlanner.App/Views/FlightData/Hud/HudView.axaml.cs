using System.ComponentModel;
using Avalonia.Interactivity;
using Avalonia.Controls;

namespace MissionPlanner.App.Views.FlightData.Hud;

/// <summary>The heads-up display of the Flight Data page.</summary>
public partial class HudView : UserControlViewBase<HudViewModel>
{
    /// <summary>Creates the view.</summary>
    public HudView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            return;
        }
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Canvas.Update(ViewModel);
        UpdateAttitudeLayout();
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.OnUnloaded(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Canvas.Update(ViewModel);
        if (e.PropertyName == nameof(HudViewModel.SelectedDisplay)) UpdateAttitudeLayout();
    }

    private void OnAttitudeSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateAttitudeLayout();

    private (bool Hud, bool Frame, bool Wide)? arrangement;

    private void UpdateAttitudeLayout()
    {
        var model = DataContext as HudViewModel;
        var showHud = model?.ShowHud ?? true;
        var showFrame = model?.ShowFrame ?? true;
        // Use the available aspect ratio, including the short docked HUD section.
        var wide = AttitudeViews.Bounds.Width >= AttitudeViews.Bounds.Height;
        if (arrangement == (showHud, showFrame, wide)) return;
        arrangement = (showHud, showFrame, wide);
        var both = showHud && showFrame;
        AttitudeViews.RowDefinitions = new RowDefinitions(both && !wide ? "*,*" : "*");
        AttitudeViews.ColumnDefinitions = new ColumnDefinitions(both && wide ? "*,*" : "*");
        Grid.SetRow(FrameCanvas, both && !wide ? 1 : 0);
        Grid.SetColumn(FrameCanvas, both && wide ? 1 : 0);
    }
}

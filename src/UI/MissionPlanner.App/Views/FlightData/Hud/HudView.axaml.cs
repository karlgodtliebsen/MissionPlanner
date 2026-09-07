using System.ComponentModel;
using Avalonia.Interactivity;

namespace MissionPlanner.App.Views.FlightData.Hud;

public partial class HudView : UserControlViewBase<HudViewModel>
{
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
    }
}

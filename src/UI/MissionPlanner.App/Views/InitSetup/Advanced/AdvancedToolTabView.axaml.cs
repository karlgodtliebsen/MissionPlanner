using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MissionPlanner.App.Views.InitSetup.Advanced;

/// <summary>Hosts only the selected, available tool and lets its existing view own activation.</summary>
public partial class AdvancedToolTabView : UserControl
{
    private AdvancedToolCardViewModel? observed;
    private Page? page;
    private bool loaded;

    /// <summary>Initializes the tab without constructing a tool or resolving services.</summary>
    public AdvancedToolTabView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (Design.IsDesignMode)
        {
            return;
        }
        loaded = true;
        ObserveSelection();
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        loaded = false;
        Detach();
        ToolContent.Content = null;
        page = null;
        base.OnUnloaded(e);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (loaded)
        {
            ObserveSelection();
        }
    }

    private void ObserveSelection()
    {
        Detach();
        ToolContent.Content = null;
        page = null;
        observed = DataContext as AdvancedToolCardViewModel;
        if (observed is not null)
        {
            observed.PropertyChanged += AvailabilityChanged;
        }
        UpdateContent();
    }

    private void Detach()
    {
        if (observed is not null)
        {
            observed.PropertyChanged -= AvailabilityChanged;
            observed = null;
        }
    }

    private void AvailabilityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvancedToolCardViewModel.Availability))
        {
            UpdateContent();
        }
    }

    private void UpdateContent()
    {
        if (observed?.Availability.CanLaunch != true)
        {
            // Removing the view also releases its telemetry and output subscriptions.
            ToolContent.Content = null;
            return;
        }
        page ??= ServiceHelper.GetRequiredService<AdvancedToolRegistry>().Create(observed.Feature.Route);
        ToolContent.Content = page;
    }
}

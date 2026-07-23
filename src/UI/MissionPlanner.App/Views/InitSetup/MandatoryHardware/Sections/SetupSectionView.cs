namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>
/// Provides visibility and page-lifecycle coordination for a setup section while the
/// concrete view retains ownership of its ViewModel.
/// </summary>
public abstract class SetupSectionView : ContentView
{
    private SetupWorkflowDetailViewModel? ownedViewModel;

    /// <summary>Initializes a setup section view.</summary>
    protected SetupSectionView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Gets or sets the ViewModel owned by the concrete section view.</summary>
    protected SetupWorkflowDetailViewModel? OwnedViewModel
    {
        get => ownedViewModel;
        set
        {
            ownedViewModel = value;
            UpdateActivation();
        }
    }

    /// <summary>Activates the owned ViewModel when this section is visible.</summary>
    public void Activate()
    {
        UpdateActivation();
    }

    /// <summary>Deactivates the owned ViewModel.</summary>
    public void Deactivate()
    {
        ownedViewModel?.Deactivate();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsVisibleProperty.PropertyName)
        {
            UpdateActivation();
        }
    }

    private void OnLoaded(object? sender, EventArgs args)
    {
        UpdateActivation();
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        Deactivate();
    }

    private void UpdateActivation()
    {
        if (IsLoaded && IsVisible)
        {
            ownedViewModel?.Activate();
        }
        else
        {
            ownedViewModel?.Deactivate();
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Material.TabViews;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class TabViewContent3 : TabViewLifecycleContent<TabViewModel3>
{
    public TabViewContent3()
    {
        InitializeComponent();
    }
}

public partial class TabViewModel3 : ObservableObject, IDisposable, IActivationLifeCycle
{
    [ObservableProperty] public partial string Name { get; set; } = "View 3";


    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }
}

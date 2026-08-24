using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Material.TabViews;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class TabViewContent2 : TabViewLifecycleContent<TabViewModel2>
{
    public TabViewContent2()
    {
        InitializeComponent();
    }
}

public partial class TabViewModel2 : ObservableObject, IDisposable, IActivationLifeCycle
{
    [ObservableProperty] public partial string Name { get; set; } = "View 2";


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

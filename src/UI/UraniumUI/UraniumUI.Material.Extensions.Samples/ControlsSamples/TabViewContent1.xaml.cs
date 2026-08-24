using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Material.TabViews;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class TabViewContent1 : TabViewLifecycleContent<TabViewModel1>
{
    public TabViewContent1()
    {
        InitializeComponent();
    }
}

public partial class TabViewModel1 : ObservableObject, IDisposable, IActivationLifeCycle
{
    [ObservableProperty] public partial string Name { get; set; } = "View 1";

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

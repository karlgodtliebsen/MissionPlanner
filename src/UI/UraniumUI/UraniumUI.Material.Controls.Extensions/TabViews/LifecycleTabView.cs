using System.Diagnostics;
using UraniumUI.Material.Controls;

namespace UraniumUI.Material.TabViews;

/// <summary>Owns the lifecycle of the selected tab content.</summary>
public class LifecycleTabView : TabView
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private View? activeContent;
    private bool isLoaded;
    private long transitionVersion;

    /// <summary>Initializes lifecycle handling at selection and visual-tree boundaries.</summary>
    public LifecycleTabView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    protected override async Task OnSelectedTabChanged(TabItem oldValue, TabItem newValue)
    {
        await base.OnSelectedTabChanged(oldValue, newValue).ConfigureAwait(true);
        transitionVersion++;
        await ReconcileLifecycleAsync().ConfigureAwait(true);
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (isLoaded) return;
        isLoaded = true;
        transitionVersion++;
        await ReconcileLifecycleFromEventAsync().ConfigureAwait(true);
    }

    private async void OnUnloaded(object? sender, EventArgs e)
    {
        if (!isLoaded) return;
        isLoaded = false;
        transitionVersion++;
        await ReconcileLifecycleFromEventAsync().ConfigureAwait(true);
    }

    private async Task ReconcileLifecycleFromEventAsync()
    {
        try
        {
            await ReconcileLifecycleAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Tab lifecycle transition failed: {exception}");
        }
    }

    private async Task ReconcileLifecycleAsync()
    {
        await lifecycleGate.WaitAsync().ConfigureAwait(true);
        try
        {
            while (true)
            {
                var observedVersion = transitionVersion;
                var desiredContent = isLoaded ? SelectedTab?.Content : null;
                if (!ReferenceEquals(activeContent, desiredContent))
                {
                    if (activeContent is IActivationLifeCycle oldLifecycle)
                    {
                        await oldLifecycle.DeactivateAsync().ConfigureAwait(true);
                    }
                    activeContent = null;

                    desiredContent = isLoaded ? SelectedTab?.Content : null;
                    if (desiredContent is IActivationLifeCycle newLifecycle)
                    {
                        await newLifecycle.ActivateAsync().ConfigureAwait(true);
                    }
                    activeContent = desiredContent;
                }

                if (observedVersion == transitionVersion &&
                    ReferenceEquals(activeContent, isLoaded ? SelectedTab?.Content : null))
                {
                    return;
                }
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }
}

using UraniumUI.Material.Controls;

namespace UraniumUI.Material.TabViews;

/// <summary>Represents a tab view control that owns the lifecycle of its selected content.</summary>
public class LifecycleTabView : TabView
{
    private View? currentContent;
    private bool isLoaded;

    /// <summary>Initializes lifecycle handling at selection and visual-tree boundaries.</summary>
    public LifecycleTabView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    protected override async Task OnSelectedTabChanged(TabItem oldValue, TabItem newValue)
    {
        // RecreateAlways clears oldValue.Content inside the base implementation. Capture
        // and deactivate it before that happens rather than using SelectedTabChanged.
        var oldContent = oldValue?.Content ?? currentContent;
        if (oldContent is not null && oldValue != newValue)
        {
            oldContent.IsEnabled = false;
            (oldContent as ITabViewLifecycleContent)?.Deactivate();
        }

        await base.OnSelectedTabChanged(oldValue, newValue).ConfigureAwait(true);

        currentContent = newValue?.Content;
        if (currentContent is null)
        {
            return;
        }

        currentContent.IsEnabled = true;
        if (isLoaded)
        {
            (currentContent as ITabViewLifecycleContent)?.Activate();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;
        currentContent ??= SelectedTab?.Content;
        if (currentContent is not null)
        {
            currentContent.IsEnabled = true;
            (currentContent as ITabViewLifecycleContent)?.Activate();
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (!isLoaded)
        {
            return;
        }

        isLoaded = false;
        if (currentContent is not null)
        {
            currentContent.IsEnabled = false;
            (currentContent as ITabViewLifecycleContent)?.Deactivate();
        }
    }
}

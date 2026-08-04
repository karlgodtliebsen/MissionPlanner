using UraniumUI.Material.Controls;

namespace UraniumUI.Material.TabViews;

/// <summary>
/// Represents a tab view control that supports lifecycle events for its tabs.
/// </summary>
public class LifecycleTabView : TabView
{
    private TabItem? current;

    /// <inheritdoc />
    public LifecycleTabView()
    {
        SelectedTabChanged += LifecycleTabView_SelectedTabChanged;
    }

    private void LifecycleTabView_SelectedTabChanged(object? sender, TabItem e)
    {
        if (current != null)
        {
            var lifecycleContent = current.Content as ITabViewLifecycleContent;
            lifecycleContent?.Deactivate();
        }

        var newLifecycleContent = e.Content as ITabViewLifecycleContent;
        newLifecycleContent?.Activate();
        current = e;
    }
}

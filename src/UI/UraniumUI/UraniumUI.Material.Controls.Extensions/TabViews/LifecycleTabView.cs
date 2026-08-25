using UraniumUI.Material.Controls;

namespace UraniumUI.Material.TabViews;

/// <summary>
/// Represents a tab view control that owns the lifecycle of its selected content.
/// </summary>
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
        var oldContent = oldValue?.Content ?? currentContent;
        if (oldContent is not null && oldValue != newValue)
        {
            if (oldContent is not IActivationLifeCycle content)
            {
                return;
            }

            await content.DeactivateAsync();
        }

        await base.OnSelectedTabChanged(oldValue, newValue).ConfigureAwait(true);

        currentContent = newValue?.Content;
        if (currentContent is null)
        {
            return;
        }

        if (isLoaded)
        {
            if (currentContent is not IActivationLifeCycle content)
            {
                return;
            }

            await content.ActivateAsync();
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
            if (currentContent is not IActivationLifeCycle content)
            {
                return;
            }
            content.ActivateAsync().GetAwaiter().GetResult();
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
            if (currentContent is not IActivationLifeCycle content)
            {
                return;
            }
            content.DeactivateAsync().GetAwaiter().GetResult();
        }
    }
}

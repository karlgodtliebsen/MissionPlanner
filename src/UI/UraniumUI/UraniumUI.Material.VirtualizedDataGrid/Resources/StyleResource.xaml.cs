using UraniumUI.Material.Resources;

namespace UraniumUI.Material.VirtualizedDataGrid.Resources;

/// <summary>
/// Provides the default Material styles for the virtualized data grid.
/// </summary>
public partial class StyleResource : ResourceDictionary
{
    private readonly ColorResource colors = new();
    private ResourceDictionary? colorsOverride;

    /// <summary>
    /// Initializes a new instance of the <see cref="StyleResource"/> class.
    /// </summary>
    public StyleResource()
    {
        // StaticResource lookups are resolved while this dictionary is being
        // initialized. A sibling dictionary in App.xaml is not in scope yet,
        // so mirror UraniumUI.Material.Resources.StyleResource and provide the
        // Material color tokens inside this dictionary.
        MergedDictionaries.Add(colors);
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets application-specific overrides for the Material colors.
    /// </summary>
    public ResourceDictionary? ColorsOverride
    {
        get => colorsOverride;
        set
        {
            colorsOverride = value;
            ApplyColorOverrides();
        }
    }

    private void ApplyColorOverrides()
    {
        if (colorsOverride is null)
        {
            return;
        }

        foreach (var key in colorsOverride.Keys)
        {
            if (colors.TryGetValue(key, out var existing) &&
                existing is Color &&
                colorsOverride[key] is Color overrideColor)
            {
                colors[key] = overrideColor;
            }
        }

        // StaticResource values were captured during XAML initialization.
        // Reload the style dictionary so its setters use the overridden colors.
        if (MergedDictionaries.Count > 1)
        {
            MergedDictionaries.Remove(MergedDictionaries.Last());
        }

        InitializeComponent();
    }
}

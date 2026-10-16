using Avalonia.Styling;

namespace MissionPlanner.Themes;

/// <summary>Named appearance variants with ordinary Semi light and dark fallbacks.</summary>
public static class MissionPlannerThemeVariants
{
    /// <summary>Gets the sampled orange palette on ordinary dark surfaces.</summary>
    public static ThemeVariant DarkOrange { get; } = new("DarkOrange", ThemeVariant.Dark);

    /// <summary>Gets the independently validated orange palette on light surfaces.</summary>
    public static ThemeVariant LightOrange { get; } = new("LightOrange", ThemeVariant.Light);

    /// <summary>Determines whether a variant inherits dark appearance, including named variants.</summary>
    public static bool IsDark(ThemeVariant variant)
    {
        for (ThemeVariant? current = variant; current is not null; current = current.InheritVariant)
        {
            if (current == ThemeVariant.Dark)
            {
                return true;
            }
        }
        return false;
    }
}

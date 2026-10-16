using Avalonia.Styling;

namespace MissionPlanner.Themes;

/// <summary>Named appearance variants with ordinary Semi light and dark fallbacks.</summary>
public static class MissionPlannerThemeVariants
{
    /// <summary>Gets the orange palette on ordinary dark surfaces.</summary>
    public static ThemeVariant DarkOrange { get; } = new("DarkOrange", ThemeVariant.Dark);

    /// <summary>Gets the orange palette on ordinary light surfaces.</summary>
    public static ThemeVariant LightOrange { get; } = new("LightOrange", ThemeVariant.Light);

    /// <summary>Gets the purple palette on the same shared dark resources.</summary>
    public static ThemeVariant DarkPurple { get; } = new("DarkPurple", ThemeVariant.Dark);

    /// <summary>Gets the purple palette on the same shared light resources.</summary>
    public static ThemeVariant LightPurple { get; } = new("LightPurple", ThemeVariant.Light);

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

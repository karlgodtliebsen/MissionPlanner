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

    /// <summary>Gets the red palette on shared dark resources.</summary>
    public static ThemeVariant DarkRed { get; } = new("DarkRed", ThemeVariant.Dark);

    /// <summary>Gets the red palette on shared light resources.</summary>
    public static ThemeVariant LightRed { get; } = new("LightRed", ThemeVariant.Light);

    /// <summary>Gets the pink palette on shared dark resources.</summary>
    public static ThemeVariant DarkPink { get; } = new("DarkPink", ThemeVariant.Dark);

    /// <summary>Gets the pink palette on shared light resources.</summary>
    public static ThemeVariant LightPink { get; } = new("LightPink", ThemeVariant.Light);

    /// <summary>Gets the violet palette on shared dark resources.</summary>
    public static ThemeVariant DarkViolet { get; } = new("DarkViolet", ThemeVariant.Dark);

    /// <summary>Gets the violet palette on shared light resources.</summary>
    public static ThemeVariant LightViolet { get; } = new("LightViolet", ThemeVariant.Light);

    /// <summary>Gets the indigo palette on shared dark resources.</summary>
    public static ThemeVariant DarkIndigo { get; } = new("DarkIndigo", ThemeVariant.Dark);

    /// <summary>Gets the indigo palette on shared light resources.</summary>
    public static ThemeVariant LightIndigo { get; } = new("LightIndigo", ThemeVariant.Light);

    /// <summary>Gets the yellow palette on shared dark resources.</summary>
    public static ThemeVariant DarkYellow { get; } = new("DarkYellow", ThemeVariant.Dark);

    /// <summary>Gets the yellow palette on shared light resources.</summary>
    public static ThemeVariant LightYellow { get; } = new("LightYellow", ThemeVariant.Light);

    /// <summary>Gets the lime palette on shared dark resources.</summary>
    public static ThemeVariant DarkLime { get; } = new("DarkLime", ThemeVariant.Dark);

    /// <summary>Gets the lime palette on shared light resources.</summary>
    public static ThemeVariant LightLime { get; } = new("LightLime", ThemeVariant.Light);

    /// <summary>Gets the light green palette on shared dark resources.</summary>
    public static ThemeVariant DarkLightGreen { get; } = new("DarkLightGreen", ThemeVariant.Dark);

    /// <summary>Gets the light green palette on shared light resources.</summary>
    public static ThemeVariant LightLightGreen { get; } = new("LightLightGreen", ThemeVariant.Light);

    /// <summary>Gets the light blue palette on shared dark resources.</summary>
    public static ThemeVariant DarkLightBlue { get; } = new("DarkLightBlue", ThemeVariant.Dark);

    /// <summary>Gets the light blue palette on shared light resources.</summary>
    public static ThemeVariant LightLightBlue { get; } = new("LightLightBlue", ThemeVariant.Light);

    /// <summary>Gets the teal palette on shared dark resources.</summary>
    public static ThemeVariant DarkTeal { get; } = new("DarkTeal", ThemeVariant.Dark);

    /// <summary>Gets the teal palette on shared light resources.</summary>
    public static ThemeVariant LightTeal { get; } = new("LightTeal", ThemeVariant.Light);

    /// <summary>Gets the cyan palette on shared dark resources.</summary>
    public static ThemeVariant DarkCyan { get; } = new("DarkCyan", ThemeVariant.Dark);

    /// <summary>Gets the cyan palette on shared light resources.</summary>
    public static ThemeVariant LightCyan { get; } = new("LightCyan", ThemeVariant.Light);

    /// <summary>Gets the green palette on shared dark resources.</summary>
    public static ThemeVariant DarkGreen { get; } = new("DarkGreen", ThemeVariant.Dark);

    /// <summary>Gets the green palette on shared light resources.</summary>
    public static ThemeVariant LightGreen { get; } = new("LightGreen", ThemeVariant.Light);

    /// <summary>Gets the grey palette on shared dark resources.</summary>
    public static ThemeVariant DarkGrey { get; } = new("DarkGrey", ThemeVariant.Dark);

    /// <summary>Gets the grey palette on shared light resources.</summary>
    public static ThemeVariant LightGrey { get; } = new("LightGrey", ThemeVariant.Light);

    /// <summary>Gets the ai purple palette on shared dark resources.</summary>
    public static ThemeVariant DarkAIPurple { get; } = new("DarkAIPurple", ThemeVariant.Dark);

    /// <summary>Gets the ai purple palette on shared light resources.</summary>
    public static ThemeVariant LightAIPurple { get; } = new("LightAIPurple", ThemeVariant.Light);

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

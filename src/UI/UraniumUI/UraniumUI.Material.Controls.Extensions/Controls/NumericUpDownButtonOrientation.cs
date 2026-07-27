#nullable enable

namespace UraniumUI.Material.Controls;

/// <summary>
/// Determines how the decrement and increment buttons are arranged inside a
/// <see cref="NumericUpDownField"/>.
/// </summary>
public enum NumericUpDownButtonOrientation
{
    /// <summary>
    /// Decrement and increment are placed side-by-side: − ＋.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Increment is placed above decrement: ＋ over −.
    /// </summary>
    Vertical
}

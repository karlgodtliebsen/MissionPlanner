using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises installed Semi and Ursa templates against the composed palette resources.</summary>
public partial class PalettePreview : UserControl
{
    /// <summary>Loads controls whose resource references remain live while switching palettes.</summary>
    public PalettePreview()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

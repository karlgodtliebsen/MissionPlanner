using Avalonia.Styling;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public class ThemeItem(string name, ThemeVariant theme)
{
    public string Name { get; set; } = name;
    public ThemeVariant Theme { get; set; } = theme;
}
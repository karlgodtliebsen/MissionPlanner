namespace MissionPlanner.App.Views.Navigation;

/// <summary>A page supporting selection of a named section through a route fragment.</summary>
public interface ISectionNavigationPage
{
    /// <summary>Selects a section of the page.</summary>
    void SelectSection(string section);
}

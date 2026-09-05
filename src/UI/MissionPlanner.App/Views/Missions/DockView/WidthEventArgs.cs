namespace MissionPlanner.App.Views.Missions.DockView;

/// <summary>
/// Provides data for the WidthChanged event.
/// </summary>
public class WidthEventArgs : EventArgs
{
    /// <summary>
    /// Gets the width.
    /// </summary>
    public double Width
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the view is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WidthEventArgs"/> class.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="isExpanded">A value indicating whether the view is expanded.</param>
    public WidthEventArgs(double width, bool isExpanded)
    {
        Width = width;
        IsExpanded = isExpanded;
    }
}

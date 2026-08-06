namespace MissionPlanner.App.Views.Missions.DockView;

public class WidthEventArgs : EventArgs
{
    public double Width { get; }
    public bool IsExpanded { get; }

    public WidthEventArgs(double width, bool isExpanded)
    {
        Width = width;
        IsExpanded = isExpanded;
    }
}

namespace UraniumUI.Material.TabViews;

/// <summary>
/// Interface for a ContentView that participates in the lifecycle of a TabView.
/// </summary>
public interface ITabViewLifecycleContent
{
    /// <summary>
    /// 
    /// </summary>
    void Activate();

    /// <summary>
    /// 
    /// </summary>
    void Deactivate();
}

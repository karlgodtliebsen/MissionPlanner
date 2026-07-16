namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Event arguments for mission-related events, containing a message describing the event. 
/// </summary>
/// <param name="message"></param>
public class MissionEventArgs(string message) : EventArgs
{
    /// <summary>
    /// Gets the message associated with the mission event.
    /// </summary>
    public string Message => message;
}

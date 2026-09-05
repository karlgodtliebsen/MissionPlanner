namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// Identifies the source stream of an item displayed in the Messages tab.
/// </summary>
public enum MessageListOrigin
{
    /// <summary>The message originated as MAVLink STATUSTEXT.</summary>
    MavLink,

    /// <summary>The message originated in an application workflow.</summary>
    Application
}


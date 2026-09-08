using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Represents an event to display the editor.
/// </summary>
public class EditorDisplayEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorDisplayEvent"/> class.
    /// </summary>
    /// <param name="name">The name of the event.</param>
    public EditorDisplayEvent(string name) : base(name)
    {
    }
}

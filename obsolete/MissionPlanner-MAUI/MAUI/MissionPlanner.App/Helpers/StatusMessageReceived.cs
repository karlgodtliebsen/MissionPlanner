using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.App.Helpers;

/// <summary>
/// Event that is triggered when a status message is received.
/// </summary>
public class StatusMessageReceived : DomainEvent<string?>
{
    /// <inheritdoc />
    public StatusMessageReceived(string? data) : base(nameof(StatusMessageReceived), data)
    {
    }

    /// <summary>
    /// Gets the status message associated with the domain event.
    /// </summary>
    public string? Message => Payload as string;

}


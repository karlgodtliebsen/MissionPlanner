using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.App.Views.Exit;

/// <summary>
/// Domain event published when a connection attempt fails.
/// </summary>
public class ExitApplicationRequested : DomainEvent<string>
{
    /// <inheritdoc />
    public ExitApplicationRequested() : base("ExitApplicationRequested", "ExitApplicationRequested")
    {
    }
}

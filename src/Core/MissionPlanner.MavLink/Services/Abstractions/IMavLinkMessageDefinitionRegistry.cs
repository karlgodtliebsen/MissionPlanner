using MissionPlanner.MavLink.Messages;
using System.Diagnostics.CodeAnalysis;

namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>
/// Provides wire definitions for all messages in the selected MAVLink dialect.
/// </summary>
public interface IMavLinkMessageDefinitionRegistry
{
    /// <summary>
    /// Gets all resolved message definitions in message-ID order.
    /// </summary>
    IReadOnlyCollection<MavLinkMessageDefinition> Definitions { get; }

    /// <summary>
    /// Tries to get the definition for a message identifier.
    /// </summary>
    /// <param name="messageId">The numeric MAVLink message identifier.</param>
    /// <param name="definition">The resolved message definition when found.</param>
    /// <returns><see langword="true"/> when the message is in the selected dialect.</returns>
    bool TryGet(uint messageId, [NotNullWhen(true)] out MavLinkMessageDefinition? definition);
}

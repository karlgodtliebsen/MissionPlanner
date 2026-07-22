using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Provides the generated definitions for the pinned MAVLink dialect.
/// </summary>
public sealed class MavLinkMessageDefinitionRegistry : IMavLinkMessageDefinitionRegistry
{
    private static readonly FrozenDictionary<uint, MavLinkMessageDefinition> DefinitionsById =
        GeneratedMavLinkMessageDefinitions.All.ToFrozenDictionary(definition => definition.MessageId);

    /// <inheritdoc />
    public IReadOnlyCollection<MavLinkMessageDefinition> Definitions => GeneratedMavLinkMessageDefinitions.All;

    /// <inheritdoc />
    public bool TryGet(
        uint messageId,
        [NotNullWhen(true)] out MavLinkMessageDefinition? definition) =>
        DefinitionsById.TryGetValue(messageId, out definition);
}

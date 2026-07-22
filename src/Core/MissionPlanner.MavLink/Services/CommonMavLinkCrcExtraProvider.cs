using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Provides CRC extra bytes from the generated MAVLink message-definition registry.
/// </summary>
public sealed class CommonMavLinkCrcExtraProvider : IMavLinkCrcExtraProvider
{
    private readonly IMavLinkMessageDefinitionRegistry registry;

    /// <summary>
    /// Initializes a compatibility provider backed by the generated dialect registry.
    /// </summary>
    public CommonMavLinkCrcExtraProvider()
        : this(new MavLinkMessageDefinitionRegistry())
    {
    }

    /// <summary>
    /// Initializes a compatibility provider backed by the specified registry.
    /// </summary>
    /// <param name="registry">The MAVLink message-definition registry.</param>
    public CommonMavLinkCrcExtraProvider(IMavLinkMessageDefinitionRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc/>
    public bool TryGetCrcExtra(uint messageId, out byte crcExtra)
    {
        if (registry.TryGet(messageId, out var definition))
        {
            crcExtra = definition.CrcExtra;
            return true;
        }

        crcExtra = 0;
        return false;
    }
}

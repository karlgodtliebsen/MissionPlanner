using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Protocol;

namespace MissionPlanner.Firmware.Discovery;

/// <summary>Owns an identified bootloader and its OS device identity.</summary>
public sealed class DiscoveredBootloader : IAsyncDisposable
{
    /// <summary>Initializes an identified bootloader result.</summary>
    public DiscoveredBootloader(SerialDeviceDescriptor device, BootloaderIdentity identity, IArduPilotBootloaderClient client)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>Gets the OS serial device descriptor.</summary>
    public SerialDeviceDescriptor Device { get; }

    /// <summary>Gets the protocol-confirmed bootloader identity.</summary>
    public BootloaderIdentity Identity { get; }

    /// <summary>Gets the identified client retained for the operation.</summary>
    public IArduPilotBootloaderClient Client { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return Client.DisposeAsync();
    }
}

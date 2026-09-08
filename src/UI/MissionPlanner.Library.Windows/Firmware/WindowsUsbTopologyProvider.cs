using System.Runtime.InteropServices;
using System.Text;
using MissionPlanner.Firmware.Betaflight;

namespace MissionPlanner.Library.Windows.Firmware;

/// <summary>Reads Windows physical USB port paths through Configuration Manager.</summary>
public sealed class WindowsUsbTopologyProvider : IUsbTopologyProvider
{
    /// <inheritdoc />
    public Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(instanceId) || CM_Locate_DevNodeW(out var node, instanceId, 0) != 0)
        {
            return Task.FromResult<string?>(null);
        }
        var key = new PropertyKey { Format = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), Id = 37 };
        var buffer = new byte[16384];
        uint length = (uint)buffer.Length;
        if (CM_Get_DevNode_PropertyW(node, ref key, out var type, buffer, ref length, 0) != 0 ||
            type != 0x2012 || length > buffer.Length || length % 2 != 0)
        {
            return Task.FromResult<string?>(null);
        }
        var paths = Encoding.Unicode.GetString(buffer, 0, (int)length).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var path = paths.FirstOrDefault(value => value.Contains("#USB(", StringComparison.OrdinalIgnoreCase));
        if (path is not null)
        {
            var interfaceOffset = path.IndexOf("#USBMI(", StringComparison.OrdinalIgnoreCase);
            if (interfaceOffset >= 0)
            {
                path = path[..interfaceOffset];
            }
        }
        return Task.FromResult(path);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint node, string instanceId, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_PropertyW(uint node, ref PropertyKey key, out uint type,
        [Out] byte[] buffer, ref uint length, uint flags);
}

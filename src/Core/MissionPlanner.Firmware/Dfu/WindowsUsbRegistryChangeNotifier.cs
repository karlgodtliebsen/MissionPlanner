using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Uses Windows registry change notification with a polling deadline fallback.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUsbRegistryChangeNotifier : IWindowsUsbDeviceChangeNotifier
{
    /// <inheritdoc />
    public async Task<bool> WaitForChangeAsync(TimeSpan pollingDeadline, CancellationToken cancellationToken = default)
    {
        if (pollingDeadline <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollingDeadline));
        try
        {
            using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
            if (usb is null)
            {
                await Task.Delay(pollingDeadline, cancellationToken).ConfigureAwait(false);
                return false;
            }

            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset);
            const uint nameOrValueChanged = 0x00000001 | 0x00000004;
            var error = RegNotifyChangeKeyValue(usb.Handle, true, nameOrValueChanged, signal.SafeWaitHandle, true);
            if (error != 0)
            {
                await Task.Delay(pollingDeadline, cancellationToken).ConfigureAwait(false);
                return false;
            }

            var signalled = await Task.Run(() =>
                WaitHandle.WaitAny([signal, cancellationToken.WaitHandle], pollingDeadline) == 0).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return signalled;
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(pollingDeadline, cancellationToken).ConfigureAwait(false);
            return false;
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegNotifyChangeKeyValue(
        SafeRegistryHandle key,
        [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
        uint notifyFilter,
        SafeWaitHandle eventHandle,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}

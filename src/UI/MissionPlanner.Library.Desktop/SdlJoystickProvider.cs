using System.Globalization;
using Avalonia.Threading;
using MissionPlanner.Core.Setup.OptionalHardware;
using Silk.NET.SDL;

namespace MissionPlanner.Library.Desktop;

/// <summary>Reads SDL joysticks on Avalonia's UI thread on Windows, Linux and macOS.</summary>
public sealed class SdlJoystickProvider : IJoystickProvider, IDisposable
{
    private Sdl? api;
    private bool disposed;
    private readonly HashSet<Device> devices = [];
    /// <inheritdoc />
    public bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    /// <inheritdoc />
    public async Task<IReadOnlyList<JoystickDeviceDescriptor>> EnumerateAsync(CancellationToken cancellationToken) =>
        await Dispatcher.UIThread.InvokeAsync(() => Enumerate(cancellationToken));

    /// <inheritdoc />
    public async Task<IJoystickDevice> OpenAsync(string deviceId, CancellationToken cancellationToken) =>
        await Dispatcher.UIThread.InvokeAsync(() => Open(deviceId, cancellationToken));

    private Sdl Required()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (api is not null) return api;
        var candidate = Sdl.GetApi();
        // Initialize only joystick support; Avalonia owns windows and the desktop event loop.
        if (candidate.InitSubSystem(0x00000200) != 0)
        {
            candidate.Dispose();
            throw new IOException("SDL joystick initialization failed. Check the local device drivers and permissions.");
        }
        candidate.JoystickEventState(0);
        api = candidate;
        return api;
    }

    private unsafe IReadOnlyList<JoystickDeviceDescriptor> Enumerate(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var sdl = Required();
        var result = new List<JoystickDeviceDescriptor>();
        sdl.JoystickUpdate();
        for (var index = 0; index < System.Math.Min(sdl.NumJoysticks(), 32); index++)
        {
            var pointer = sdl.JoystickOpen(index);
            if (pointer is null) continue;
            try
            {
                var axes = sdl.JoystickNumAxes(pointer);
                var buttons = sdl.JoystickNumButtons(pointer);
                result.Add(new(sdl.JoystickGetDeviceInstanceID(index).ToString(CultureInfo.InvariantCulture),
                    sdl.JoystickNameS(pointer) ?? "Joystick", axes, buttons, axes is >= 4 and <= 64 && buttons is >= 1 and <= 128));
            }
            finally { sdl.JoystickClose(pointer); }
        }
        return result;
    }

    private unsafe IJoystickDevice Open(string id, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var sdl = Required();
        for (var index = 0; index < sdl.NumJoysticks(); index++)
        {
            if (sdl.JoystickGetDeviceInstanceID(index).ToString(CultureInfo.InvariantCulture) != id) continue;
            var pointer = sdl.JoystickOpen(index);
            if (pointer is null) throw new IOException("The joystick could not be opened.");
            var descriptor = new JoystickDeviceDescriptor(id, sdl.JoystickNameS(pointer) ?? "Joystick",
                sdl.JoystickNumAxes(pointer), sdl.JoystickNumButtons(pointer));
            if (descriptor.AxisCount is < 4 or > 64 || descriptor.ButtonCount is < 1 or > 128)
            {
                sdl.JoystickClose(pointer);
                throw new NotSupportedException("A joystick with 4–64 axes and 1–128 buttons is required.");
            }
            var device = new Device(this, (nint)pointer, descriptor);
            devices.Add(device);
            return device;
        }
        throw new IOException("The selected joystick disconnected. Refresh the device list.");
    }

    private unsafe JoystickState Read(Device device, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var sdl = Required();
        if (device.Handle == 0 || sdl.JoystickGetAttached((Joystick*)device.Handle) == SdlBool.False)
            throw new IOException("The joystick disconnected; vehicle output has stopped.");
        sdl.JoystickUpdate();
        var axes = new JoystickAxisState[device.Descriptor.AxisCount];
        var buttons = new JoystickButtonState[device.Descriptor.ButtonCount];
        for (var i = 0; i < axes.Length; i++)
        {
            var value = sdl.JoystickGetAxis((Joystick*)device.Handle, i);
            axes[i] = new(i, value < 0 ? value / 32768d : value / 32767d);
        }
        for (var i = 0; i < buttons.Length; i++) buttons[i] = new(i, sdl.JoystickGetButton((Joystick*)device.Handle, i) != 0);
        return new(device.Descriptor.Id, axes, buttons, DateTimeOffset.UtcNow);
    }

    private unsafe void Close(Device device)
    {
        if (device.Handle == 0) return;
        api?.JoystickClose((Joystick*)device.Handle);
        device.Handle = 0;
        devices.Remove(device);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        void CloseAll()
        {
            if (disposed) return;
            foreach (var device in devices.ToArray()) Close(device);
            disposed = true;
            api?.QuitSubSystem(0x00000200);
            api?.Dispose();
            api = null;
        }
        if (Dispatcher.UIThread.CheckAccess()) CloseAll();
        else Dispatcher.UIThread.Post(CloseAll);
    }

    private sealed class Device(SdlJoystickProvider owner, nint handle, JoystickDeviceDescriptor descriptor) : IJoystickDevice
    {
        internal nint Handle = handle;
        public JoystickDeviceDescriptor Descriptor => descriptor;
        public async Task<JoystickState> ReadAsync(CancellationToken cancellationToken) =>
            await Dispatcher.UIThread.InvokeAsync(() => owner.Read(this, cancellationToken));
        public async ValueTask DisposeAsync() => await Dispatcher.UIThread.InvokeAsync(() => owner.Close(this));
    }
}

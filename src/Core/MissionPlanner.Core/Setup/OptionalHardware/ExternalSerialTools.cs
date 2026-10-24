using System.Text;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Owns one direct, exclusive serial conversation.</summary>
public interface IDirectSerialSession : IAsyncDisposable
{
    /// <summary>Gets the serial port name.</summary>
    string PortName { get; }
    /// <summary>Writes text to the port.</summary>
    Task WriteAsync(string value, CancellationToken cancellationToken = default);
    /// <summary>Writes a binary protocol frame without text encoding.</summary>
    Task WriteBytesAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This serial adapter does not support binary writes.");
    /// <summary>Reads bytes into the buffer and returns the number read.</summary>
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    /// <summary>Reads one line, failing with a timeout when none arrives.</summary>
    Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Opens direct serial sessions while protecting the active MAVLink transport.</summary>
public interface IDirectSerialSessionFactory
{
    /// <summary>Opens the port exclusively.</summary>
    Task<IDirectSerialSession> OpenAsync(string portName, int baudRate, CancellationToken cancellationToken = default);
}

/// <summary>Uses the shared firmware serial factory for direct Optional Hardware tools.</summary>
public sealed class DirectSerialSessionFactory(
    IActiveVehicleContext activeVehicle,
    IVehicleConnectionSession vehicleSession,
    IFirmwareSerialPortFactory ports) : IDirectSerialSessionFactory
{
    /// <summary>Opens the port exclusively, refusing ports in use by the active MAVLink connection.</summary>
    public async Task<IDirectSerialSession> OpenAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        if (activeVehicle.IsOnline && vehicleSession.Transport is ISerialMavLinkTransport serial &&
            string.Equals(serial.PortName, portName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Disconnect the vehicle before using this direct serial tool.");
        }

        var port = await ports.OpenAsync(new SerialPortOpenOptions(portName, baudRate), cancellationToken).ConfigureAwait(false);
        return new DirectSerialSession(port);
    }

    private sealed class DirectSerialSession(IFirmwareSerialPort port) : IDirectSerialSession
    {
        private readonly StreamReader reader = new(port.Stream, Encoding.ASCII, false, 1024, true);
        public string PortName => port.PortName;

        public async Task WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            await port.Stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await port.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task WriteBytesAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            await port.Stream.WriteAsync(value, cancellationToken).ConfigureAwait(false);
            await port.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            await port.Stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        public async Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            try
            {
                return await reader.ReadLineAsync(deadline.Token).ConfigureAwait(false)
                       ?? throw new EndOfStreamException("The serial device closed the conversation.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The serial device did not respond within {timeout}.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            reader.Dispose();
            await port.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>A snapshot of a SiK radio's settings.</summary>
/// <param name="Identity">The radio identity text.</param>
/// <param name="LocalSettings">The settings of the local radio.</param>
/// <param name="RemoteSettings">The settings of the remote radio.</param>
public sealed record SikRadioSnapshot(string Identity, IReadOnlyDictionary<string, string> LocalSettings, IReadOnlyDictionary<string, string> RemoteSettings)
{
    /// <summary>Gets firmware-provided register names from ATI5/RTI5.</summary>
    public IReadOnlyDictionary<string, string> Names { get; init; } = new Dictionary<string, string>();
    /// <summary>Gets independent remote register meanings, which may differ across radio firmware versions.</summary>
    public IReadOnlyDictionary<string, string> RemoteNames { get; init; } = new Dictionary<string, string>();
}

/// <summary>Reads and writes the settings of a SiK telemetry radio.</summary>
public interface ISikRadioConfigurator
{
    /// <summary>Rechecks identity, values and register meanings before applying reviewed paired edits.</summary>
    Task ApplyReviewedAsync(string portName, int baudRate, SikRadioSnapshot expected, IReadOnlyDictionary<string, string> local,
        IReadOnlyDictionary<string, string> remote, CancellationToken cancellationToken = default) => throw new NotSupportedException("Reviewed radio editing is unavailable.");
    /// <summary>Writes and verifies explicit local/remote edits, saves each changed radio, then requests reboot.</summary>
    Task ApplyPairedAsync(string portName, int baudRate, IReadOnlyDictionary<string, string> local,
        IReadOnlyDictionary<string, string> remote, CancellationToken cancellationToken = default);
    /// <summary>Restores one radio's firmware defaults, saves and requests reboot.</summary>
    Task RestoreDefaultsAsync(string portName, int baudRate, bool remote, CancellationToken cancellationToken = default);
    /// <summary>Reads the settings of the radio.</summary>
    Task<SikRadioSnapshot> ReadAsync(string portName, int baudRate, CancellationToken cancellationToken = default);
    /// <summary>Writes settings to the radio.</summary>
    Task ApplyAsync(string portName, int baudRate, IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default);
}

/// <summary>The AT command dialect of a Bluetooth module.</summary>
public enum BluetoothAtDialect
{
    /// <summary>The dialect could not be determined.</summary>
    Unknown,
    /// <summary>HC-05 style commands.</summary>
    Hc05,
    /// <summary>HC-06 style commands.</summary>
    Hc06,
}
/// <summary>A snapshot of a Bluetooth module.</summary>
/// <param name="Dialect">The detected AT dialect.</param>
/// <param name="BaudRate">The module baud rate.</param>
/// <param name="Identity">The module identity text.</param>
public sealed record BluetoothModuleSnapshot(BluetoothAtDialect Dialect, int BaudRate, string Identity);
/// <summary>New settings for a Bluetooth module.</summary>
/// <param name="Name">The new device name, or null to keep it.</param>
/// <param name="BaudRate">The new baud rate, or null to keep it.</param>
/// <param name="Pin">The new pairing PIN, or null to keep it.</param>
public sealed record BluetoothModuleSettings(string? Name, int? BaudRate, string? Pin);

/// <summary>Reads and writes the settings of a serial Bluetooth module.</summary>
public interface IBluetoothSerialConfigurator
{
    /// <summary>Detects the module and reads its settings.</summary>
    Task<BluetoothModuleSnapshot> ProbeAsync(string portName, CancellationToken cancellationToken = default);
    /// <summary>Writes settings to the module.</summary>
    Task ApplyAsync(string portName, BluetoothModuleSnapshot module, BluetoothModuleSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Detects and configures classic HC-05/HC-06 serial Bluetooth AT dialects.</summary>
public sealed class BluetoothSerialConfigurator(IDirectSerialSessionFactory sessions) : IBluetoothSerialConfigurator
{
    private static readonly int[] probeBauds = [9600, 38400, 57600, 115200];
    private static readonly TimeSpan timeout = TimeSpan.FromMilliseconds(700);

    /// <summary>Detects the module and reads its settings.</summary>
    public async Task<BluetoothModuleSnapshot> ProbeAsync(string portName, CancellationToken cancellationToken = default)
    {
        foreach (var baud in probeBauds)
        {
            try
            {
                await using var session = await sessions.OpenAsync(portName, baud, cancellationToken).ConfigureAwait(false);
                await session.WriteAsync("AT\r\n", cancellationToken).ConfigureAwait(false);
                var response = await session.ReadLineAsync(timeout, cancellationToken).ConfigureAwait(false);
                var dialect = response.Contains("OK", StringComparison.OrdinalIgnoreCase)
                    ? baud == 38400 ? BluetoothAtDialect.Hc05 : BluetoothAtDialect.Hc06
                    : BluetoothAtDialect.Unknown;
                if (dialect != BluetoothAtDialect.Unknown) return new BluetoothModuleSnapshot(dialect, baud, response);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            catch (TimeoutException) { }
        }
        throw new InvalidOperationException("No supported classic serial Bluetooth AT module responded.");
    }

    /// <summary>Writes settings to the module.</summary>
    public async Task ApplyAsync(string portName, BluetoothModuleSnapshot module, BluetoothModuleSettings settings, CancellationToken cancellationToken = default)
    {
        await using var session = await sessions.OpenAsync(portName, module.BaudRate, cancellationToken).ConfigureAwait(false);
        foreach (var command in Commands(module.Dialect, settings))
        {
            await session.WriteAsync(command + "\r\n", cancellationToken).ConfigureAwait(false);
            _ = await session.ReadLineAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static IReadOnlyList<string> Commands(BluetoothAtDialect dialect, BluetoothModuleSettings settings)
    {
        static string Clean(string value) => value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        var commands = new List<string>();
        if (settings.Name is { Length: > 0 } name) commands.Add(dialect == BluetoothAtDialect.Hc05 ? $"AT+NAME={Clean(name)}" : $"AT+NAME{Clean(name)}");
        if (settings.Pin is { Length: > 0 } pin) commands.Add(dialect == BluetoothAtDialect.Hc05 ? $"AT+PSWD={Clean(pin)}" : $"AT+PIN{Clean(pin)}");
        if (settings.BaudRate is { } baud) commands.Add(dialect == BluetoothAtDialect.Hc05 ? $"AT+UART={baud},0,0" : $"AT+BAUD{baud}");
        return commands;
    }
}

file static class CharExtensions
{
    public static bool ContainsAny(this string value, params char[] chars) => value.IndexOfAny(chars) >= 0;
}

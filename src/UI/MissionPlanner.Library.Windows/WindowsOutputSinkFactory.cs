using System.Net;
using System.Net.Sockets;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Library.Windows;

/// <summary>Owns native output-only handles. No socket listener or receive-to-vehicle path is created.</summary>
public sealed class WindowsOutputSinkFactory(IFirmwareSerialPortFactory serialPorts,
    IActiveVehicleContext active, IVehicleConnectionSession connection, IVehicleRegistry vehicles) : IOutputSinkFactory
{
    /// <inheritdoc />
    public string? UnavailableReason(OutputEndpoint endpoint)
    {
        if (endpoint.Validate() is { } error) { return error; }
        if (endpoint.Kind == OutputEndpointKind.Serial && active.IsOnline && connection.Transport is ISerialMavLinkTransport serial
            && string.Equals(serial.PortName, endpoint.Address, StringComparison.OrdinalIgnoreCase))
        {
            return "The vehicle connection already owns this serial port. Choose a different output endpoint.";
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<IOutputSink> OpenAsync(OutputEndpoint endpoint, CancellationToken token)
    {
        if (UnavailableReason(endpoint) is { } error) { throw new InvalidOperationException(error); }
        if (endpoint.Kind == OutputEndpointKind.Serial)
        {
            return new SerialSink(await serialPorts.OpenAsync(new SerialPortOpenOptions(endpoint.Address, endpoint.BaudRate), token).ConfigureAwait(false));
        }
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Address, token).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(value => value.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault() ?? throw new IOException("No address resolved for the output host.");
        var target = new IPEndPoint(address, endpoint.Port);
        // Reject direct reflection to an observed vehicle endpoint, including a resolved DNS alias.
        if (vehicles.Vehicles.Any(vehicle => IPEndPoint.TryParse(vehicle.EndPoint.ToString(), out var source)
            && source.Port == target.Port && source.Address.MapToIPv6().Equals(target.Address.MapToIPv6())))
        {
            throw new InvalidOperationException("The output destination is an active vehicle endpoint.");
        }
        var socket = new Socket(address.AddressFamily, endpoint.Kind == OutputEndpointKind.Udp ? SocketType.Dgram : SocketType.Stream,
            endpoint.Kind == OutputEndpointKind.Udp ? ProtocolType.Udp : ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(target, token).ConfigureAwait(false);
            return new SocketSink(socket, endpoint.Kind == OutputEndpointKind.Udp);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private sealed class SocketSink(Socket socket, bool datagram) : IOutputSink
    {
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            do
            {
                var sent = await socket.SendAsync(data, SocketFlags.None, token).ConfigureAwait(false);
                if (sent == 0 || datagram && sent != data.Length) { throw new IOException("Output socket closed or rejected a datagram."); }
                data = data[sent..];
            } while (!data.IsEmpty);
        }
        public void Abort() => socket.Dispose();
        public ValueTask DisposeAsync() { socket.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class SerialSink(IFirmwareSerialPort port) : IOutputSink
    {
        private readonly Stream stream = port.Stream;
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken token) =>
            await stream.WriteAsync(data, token).ConfigureAwait(false);
        public void Abort() => stream.Dispose();
        public ValueTask DisposeAsync() => port.DisposeAsync();
    }
}

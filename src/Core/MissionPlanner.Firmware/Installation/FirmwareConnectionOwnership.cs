namespace MissionPlanner.Firmware.Installation;

/// <summary>Scopes firmware conflicts to the selected serial resource.</summary>
public static class FirmwareConnectionOwnership
{
    /// <summary>Reports a same-port or unknown-ownership conflict; network transports do not own serial ports.</summary>
    public static bool OwnsSerialPort(this IFirmwareConnectionGateway connection, string? portName)
    {
        if (connection.ActiveTransportKind is ConnectionTransportKind.Tcp or ConnectionTransportKind.Udp)
        {
            return false;
        }
        if (!connection.IsVehicleConnected && connection.ActiveTransportKind is null)
        {
            return false;
        }
        if (connection.ActiveTransportKind != ConnectionTransportKind.Serial
            || string.IsNullOrWhiteSpace(connection.ActiveSerialPort) || string.IsNullOrWhiteSpace(portName))
        {
            return true;
        }
        return string.Equals(Normalize(connection.ActiveSerialPort), Normalize(portName), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace("\\\\.\\", string.Empty, StringComparison.Ordinal);
    }
}

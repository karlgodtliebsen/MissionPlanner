namespace MissionPlanner.Core.Configuration.VendorDevices.CubeLan;

/// <summary>Implements the CubeLAN configuration format documented by the legacy plugin.</summary>
public sealed class CubeLanConfigurationCodec : ICubeLanConfigurationCodec
{
    private const byte ClassOfServicePage = 21;
    private const byte EnergyEfficientEthernetPage = 22;
    private const byte VlanPage = 23;

    /// <inheritdoc />
    public CubeLanDecodeResult Decode(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8 || buffer[0] != 0xaa || buffer[1] != 0x55)
        {
            return new CubeLanDecodeResult(false, null, "CubeLAN configuration header AA55 was not found.");
        }

        var registers = CreateDocumentedDefaults();
        var offset = 2;
        var foundEnd = false;
        while (offset + 1 < buffer.Length)
        {
            if (buffer[offset] == 0x55 && buffer[offset + 1] == 0xaa)
            {
                foundEnd = true;
                break;
            }

            if (offset + 3 >= buffer.Length)
            {
                break;
            }

            registers[(buffer[offset], buffer[offset + 1])] =
                (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]);
            offset += 4;
        }

        if (!foundEnd)
        {
            return new CubeLanDecodeResult(false, null, "CubeLAN configuration terminator 55AA was not found.");
        }

        return new CubeLanDecodeResult(true, Project(registers), null);
    }

    /// <inheritdoc />
    public byte[] Encode(CubeLanConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var registers = configuration.Registers.ToDictionary(
            item => (item.PhyAddress, item.Register),
            item => item.Value);

        foreach (var port in configuration.Ports)
        {
            SetBit(registers, ClassOfServicePage, port.PortIndex, 10, port.ClassOfServiceEnabled);
            SetBit(registers, ClassOfServicePage, port.PortIndex, 11, port.ClassOfServiceHighPriority);
            SetBit(registers, EnergyEfficientEthernetPage, 0, port.PortIndex, port.EnergyEfficientEthernetEnabled);
            SetBit(registers, VlanPage, 13, port.PortIndex, port.VlanTagged);
        }

        foreach (var membership in configuration.VlanMembership)
        {
            var register = checked((byte)(15 + membership.SourcePort / 2));
            var bit = checked((byte)(membership.DestinationPort + (membership.SourcePort % 2 == 1 ? 8 : 0)));
            SetBit(registers, VlanPage, register, bit, membership.IsMember);
        }

        var data = new List<byte>(2 + registers.Count * 4 + 6) { 0xaa, 0x55 };
        foreach (var register in registers.OrderBy(item => item.Key.Item1).ThenBy(item => item.Key.Item2))
        {
            data.Add(register.Key.Item1);
            data.Add(register.Key.Item2);
            data.Add((byte)(register.Value >> 8));
            data.Add((byte)register.Value);
        }

        data.Add(0x55);
        data.Add(0xaa);
        data.AddRange([0xff, 0xff, 0xff, 0xff]);
        return data.ToArray();
    }

    private static Dictionary<(byte Phy, byte Register), ushort> CreateDocumentedDefaults()
    {
        var registers = new Dictionary<(byte, byte), ushort>();
        for (byte port = 0; port < 8; port++)
        {
            registers[(ClassOfServicePage, port)] = 0;
        }

        registers[(EnergyEfficientEthernetPage, 0)] = 0x00ff;
        registers[(VlanPage, 13)] = 0;
        for (byte register = 15; register <= 18; register++)
        {
            registers[(VlanPage, register)] = 0xffff;
        }

        return registers;
    }

    private static CubeLanConfiguration Project(IReadOnlyDictionary<(byte Phy, byte Register), ushort> registers)
    {
        var ports = Enumerable.Range(0, 8).Select(port => new CubeLanPortConfiguration(
            (byte)port,
            GetBit(registers, ClassOfServicePage, (byte)port, 10),
            GetBit(registers, ClassOfServicePage, (byte)port, 11),
            GetBit(registers, EnergyEfficientEthernetPage, 0, (byte)port),
            GetBit(registers, VlanPage, 13, (byte)port))).ToArray();
        var membership = (from source in Enumerable.Range(0, 8)
                          from destination in Enumerable.Range(0, 8)
                          let register = (byte)(15 + source / 2)
                          let bit = (byte)(destination + (source % 2 == 1 ? 8 : 0))
                          select new CubeLanVlanMembership(
                              (byte)source,
                              (byte)destination,
                              GetBit(registers, VlanPage, register, bit))).ToArray();
        var values = registers.OrderBy(item => item.Key.Phy).ThenBy(item => item.Key.Register)
            .Select(item => new CubeLanRegisterValue(item.Key.Phy, item.Key.Register, item.Value))
            .ToArray();
        return new CubeLanConfiguration(ports, membership, values);
    }

    private static bool GetBit(
        IReadOnlyDictionary<(byte Phy, byte Register), ushort> registers,
        byte phy,
        byte register,
        byte bit) =>
        registers.TryGetValue((phy, register), out var value) && (value & (1 << bit)) != 0;

    private static void SetBit(
        IDictionary<(byte Phy, byte Register), ushort> registers,
        byte phy,
        byte register,
        byte bit,
        bool enabled)
    {
        registers.TryGetValue((phy, register), out var value);
        registers[(phy, register)] = enabled
            ? (ushort)(value | (1 << bit))
            : (ushort)(value & ~(1 << bit));
    }
}

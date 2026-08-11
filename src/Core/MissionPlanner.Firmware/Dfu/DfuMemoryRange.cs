namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains one bounded contiguous Intel HEX data range.</summary>
public sealed record DfuMemoryRange
{
    /// <summary>Initializes a range and takes an immutable copy of its data.</summary>
    public DfuMemoryRange(uint startAddress, ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("A DFU memory range cannot be empty.", nameof(data));
        }

        if ((ulong)startAddress + (ulong)data.Length - 1 > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "The DFU memory range exceeds the 32-bit address space.");
        }

        StartAddress = startAddress;
        Data = data.ToArray();
    }

    /// <summary>Gets the first represented address.</summary>
    public uint StartAddress { get; }

    /// <summary>Gets an immutable snapshot of represented bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>Gets the inclusive final represented address.</summary>
    public uint EndAddress => checked(StartAddress + (uint)Data.Length - 1);
}

namespace MissionPlanner.Core.Configuration.VendorDevices.CubeLan;

/// <summary>Encodes and decodes the repository-documented CubeLAN configuration envelope.</summary>
public interface ICubeLanConfigurationCodec
{
    /// <summary>Decodes a device buffer.</summary>
    /// <param name="buffer">The bytes returned from I²C address 0x50.</param>
    /// <returns>The decode result.</returns>
    CubeLanDecodeResult Decode(ReadOnlySpan<byte> buffer);

    /// <summary>Encodes a complete configuration document while preserving unknown registers.</summary>
    /// <param name="configuration">The validated configuration.</param>
    /// <returns>The encoded document.</returns>
    byte[] Encode(CubeLanConfiguration configuration);
}

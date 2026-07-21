using System.Buffers.Binary;
using System.Text;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Decodes the packed parameter format exposed by ArduPilot's <c>@PARAM</c> virtual file system.
/// </summary>
public sealed class ArduPilotPackedParameterDecoder : IArduPilotPackedParameterDecoder
{
    private const ushort MagicWithoutDefaults = 0x671b;
    private const ushort MagicWithDefaults = 0x671c;
    private const int HeaderLength = 6;

    /// <inheritdoc />
    public IReadOnlyList<VehicleParameter> Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            throw new InvalidDataException("ArduPilot packed parameter file is shorter than its header.");
        }

        var magic = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if (magic is not MagicWithoutDefaults and not MagicWithDefaults)
        {
            throw new InvalidDataException($"Unsupported ArduPilot packed parameter magic 0x{magic:x4}.");
        }

        var parameterCount = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        var totalParameterCount = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        var parameters = new List<VehicleParameter>(parameterCount);
        var previousName = string.Empty;
        var offset = HeaderLength;

        while (parameters.Count < parameterCount)
        {
            while (offset < data.Length && data[offset] == 0)
            {
                offset++;
            }

            EnsureAvailable(data, offset, 2);
            var typeAndFlags = data[offset++];
            var nameLengths = data[offset++];
            var packedType = (byte)(typeAndFlags & 0x0f);
            var includesDefault = (typeAndFlags & 0x10) != 0;
            var commonLength = nameLengths & 0x0f;
            var suffixLength = (nameLengths >> 4) + 1;
            var valueLength = GetValueLength(packedType);

            if (commonLength > previousName.Length)
            {
                throw new InvalidDataException("Packed parameter name prefix exceeds the previous name.");
            }

            EnsureAvailable(data, offset, suffixLength + valueLength + (includesDefault ? valueLength : 0));
            var name = previousName[..commonLength] + Encoding.ASCII.GetString(data.Slice(offset, suffixLength));
            offset += suffixLength;

            var value = DecodeValue(data.Slice(offset, valueLength), packedType);
            offset += valueLength;
            if (includesDefault)
            {
                offset += valueLength;
            }

            var index = checked((ushort)parameters.Count);
            parameters.Add(new VehicleParameter(name, value, ToMavParamType(packedType), index, totalParameterCount));
            previousName = name;
        }

        return parameters;
    }

    private static int GetValueLength(byte packedType) => packedType switch
    {
        1 => sizeof(sbyte),
        2 => sizeof(short),
        3 => sizeof(int),
        4 => sizeof(float),
        _ => throw new InvalidDataException($"Unsupported ArduPilot packed parameter type {packedType}.")
    };

    private static float DecodeValue(ReadOnlySpan<byte> data, byte packedType) => packedType switch
    {
        1 => unchecked((sbyte)data[0]),
        2 => BinaryPrimitives.ReadInt16LittleEndian(data),
        3 => BinaryPrimitives.ReadInt32LittleEndian(data),
        4 => BinaryPrimitives.ReadSingleLittleEndian(data),
        _ => throw new InvalidDataException($"Unsupported ArduPilot packed parameter type {packedType}.")
    };

    private static MavParamType ToMavParamType(byte packedType) => packedType switch
    {
        1 => MavParamType.Int8,
        2 => MavParamType.Int16,
        3 => MavParamType.Int32,
        4 => MavParamType.Real32,
        _ => throw new InvalidDataException($"Unsupported ArduPilot packed parameter type {packedType}.")
    };

    private static void EnsureAvailable(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count)
        {
            throw new InvalidDataException("ArduPilot packed parameter file ended inside a parameter record.");
        }
    }
}

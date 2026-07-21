using System.Buffers.Binary;
using FluentAssertions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests decoding of ArduPilot's packed parameter virtual file.
/// </summary>
public sealed class ArduPilotPackedParameterDecoderTests
{
    /// <summary>
    /// Verifies supported value types, prefix-compressed names, padding, and optional defaults.
    /// </summary>
    [Fact]
    public void Decode_ParsesPackedParameters()
    {
        var data = new List<byte>();
        AppendUInt16(data, 0x671c);
        AppendUInt16(data, 4);
        AppendUInt16(data, 4);
        AppendParameter(data, 1, "TEST_I8", 0, [unchecked((byte)-8)]);
        data.Add(0);
        AppendParameter(data, 2, "TEST_I16", 6, Bytes((short)-1600));
        AppendParameter(data, 3, "TEST_I32", 6, Bytes(-320_000));
        AppendParameter(data, 4, "TEST_FLOAT", 5, Bytes(12.5f), Bytes(10f));

        var result = new ArduPilotPackedParameterDecoder().Decode(data.ToArray());

        result.Should().HaveCount(4);
        result.Select(x => x.Name).Should().Equal("TEST_I8", "TEST_I16", "TEST_I32", "TEST_FLOAT");
        result.Select(x => x.Value).Should().Equal(-8f, -1600f, -320_000f, 12.5f);
        result.Select(x => x.Type).Should().Equal(MavParamType.Int8, MavParamType.Int16, MavParamType.Int32, MavParamType.Real32);
        result.Select(x => x.Index).Should().Equal((ushort)0, (ushort)1, (ushort)2, (ushort)3);
        result.Should().OnlyContain(x => x.Count == 4);
    }

    /// <summary>
    /// Verifies malformed packed files are rejected so the caller can use the MAVLink fallback.
    /// </summary>
    [Fact]
    public void Decode_RejectsInvalidMagic()
    {
        var action = () => new ArduPilotPackedParameterDecoder().Decode(new byte[6]);
        action.Should().Throw<InvalidDataException>();
    }

    private static void AppendParameter(List<byte> output, byte type, string name, int commonLength, byte[] value, byte[]? defaultValue = null)
    {
        var suffix = name[commonLength..];
        output.Add((byte)(type | (defaultValue is null ? 0 : 0x10)));
        output.Add((byte)(commonLength | ((suffix.Length - 1) << 4)));
        output.AddRange(System.Text.Encoding.ASCII.GetBytes(suffix));
        output.AddRange(value);
        if (defaultValue is not null)
        {
            output.AddRange(defaultValue);
        }
    }

    private static void AppendUInt16(List<byte> output, ushort value) => output.AddRange(Bytes(value));

    private static byte[] Bytes(short value)
    {
        var result = new byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(result, value);
        return result;
    }

    private static byte[] Bytes(ushort value)
    {
        var result = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(result, value);
        return result;
    }

    private static byte[] Bytes(int value)
    {
        var result = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(result, value);
        return result;
    }

    private static byte[] Bytes(float value)
    {
        var result = new byte[sizeof(float)];
        BinaryPrimitives.WriteSingleLittleEndian(result, value);
        return result;
    }
}

using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Setup.Advanced.Privacy;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies that privacy exports retain only explicit numeric diagnostics and relative time.</summary>
public sealed class DiagnosticPrivacyExportTests
{
    private static DiagnosticPrivacyExport Create()
    {
        var definitions = new MavLinkMessageDefinitionRegistry();
        var decoder = new MavLinkMessageDecoderHandler(new MavLinkMessageDecoders(new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions), NullLogger<MavLinkMessageDecoderHandler>.Instance);
        return new(definitions, decoder);
    }

    [Fact]
    public async Task DataFlashTextDropsLocationsParametersAndUnknownFields()
    {
        var text = "FMT, 1, 31, IMU, Qfffff, TimeUS, AccX, AccY, AccZ, Lat, Secret\n" +
            "FMT, 2, 23, GPS, Qff, TimeUS, Lat, Lng\nFMT, 3, 23, PARM, QNf, TimeUS, Name, Value\n" +
            "IMU, 900000000, 1.5, 2.5, 3.5, 55.123456, 998877\nGPS, 900000000, 55.123456, 12.654321\n" +
            "PARM, 900000000, SECRET_ID, 123456\nIMU, 901000000, 2, 3, NaN, 55, 0\n";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var output = new MemoryStream();
        var result = await Create().ExportAsync(input, ".log", output, TestContext.Current.CancellationToken);
        var csv = Encoding.UTF8.GetString(output.ToArray());
        Assert.Equal(5, result.Rows);
        Assert.Contains("0.000000,S1,IMU,AccX,1.5", csv);
        Assert.Contains("1.000000,S1,IMU,AccY,3", csv);
        Assert.DoesNotContain("Secret", csv);
        Assert.DoesNotContain("55.123456", csv);
        Assert.DoesNotContain("900000000", csv);
        Assert.DoesNotContain("GPS", csv);
        Assert.DoesNotContain("PARM", csv);
        Assert.Equal(text, Encoding.UTF8.GetString(input.ToArray()));
        Assert.All(csv.Trim().Split('\n').Skip(1), row => Assert.Equal(5, row.Split(',').Length));
    }

    [Fact]
    public async Task TelemetryRequiresCrcAndOmitsAbsoluteTimeIdentityAndGps()
    {
        using var input = new MemoryStream();
        var definitions = new MavLinkMessageDefinitionRegistry();
        void Add(byte id, byte[] payload, ulong micros, bool corrupt = false)
        {
            Assert.True(definitions.TryGet(id, out var definition));
            var packet = new byte[payload.Length + 12];
            packet[0] = 0xfd; packet[1] = (byte)payload.Length; packet[5] = 123; packet[6] = 45; packet[7] = id;
            payload.CopyTo(packet, 10);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(packet.Length - 2), MavLinkCrc.Calculate(packet.AsSpan(1, packet.Length - 3), definition!.CrcExtra));
            if (corrupt) packet[^1] ^= 1;
            var stamp = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(stamp, micros); input.Write(stamp); input.Write(packet);
        }
        var attitude = new byte[28];
        BinaryPrimitives.WriteSingleLittleEndian(attitude.AsSpan(4), .5f);
        Add(30, attitude, 1700000000000000);
        Add(24, new byte[30], 1700000000500000);
        Add(30, attitude, 1700000001000000);
        Add(30, attitude, 1700000002000000, true);
        input.Position = 0;
        using var output = new MemoryStream();
        var result = await Create().ExportAsync(input, ".tlog", output, TestContext.Current.CancellationToken);
        var csv = Encoding.UTF8.GetString(output.ToArray());
        Assert.Equal(6, result.Rows);
        Assert.Equal(2, result.ExcludedRecords);
        Assert.Contains("1.000000,S1,ATTITUDE,roll_rad,0.5", csv);
        Assert.DoesNotContain("1700000000", csv);
        Assert.DoesNotContain("123", csv);
        Assert.DoesNotContain("GPS", csv);
    }
    [Fact]
    public async Task BinaryDataFlashExportsOnlyAllowlistedFieldsAndRelativeTime()
    {
        using var input = new MemoryStream();
        void Format(byte id, byte length, string name, string types, string columns)
        {
            var frame = new byte[89]; frame[0] = 0xA3; frame[1] = 0x95; frame[2] = 128; frame[3] = id; frame[4] = length;
            Encoding.ASCII.GetBytes(name).CopyTo(frame, 5); Encoding.ASCII.GetBytes(types).CopyTo(frame, 9); Encoding.ASCII.GetBytes(columns).CopyTo(frame, 25);
            input.Write(frame);
        }
        Format(1, 31, "IMU", "Qfffff", "TimeUS,AccX,AccY,AccZ,Lat,Secret");
        Format(2, 19, "GPS", "Qff", "TimeUS,Lat,Lng");
        var imu = new byte[31]; imu[0] = 0xA3; imu[1] = 0x95; imu[2] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(imu.AsSpan(3), 900000000);
        foreach (var (offset, value) in new[] { (11, 1.25f), (15, 2.5f), (19, 3.75f), (23, 55.5f), (27, 998877f) })
            BinaryPrimitives.WriteSingleLittleEndian(imu.AsSpan(offset), value);
        input.Write(imu);
        var gps = new byte[19]; gps[0] = 0xA3; gps[1] = 0x95; gps[2] = 2;
        BinaryPrimitives.WriteUInt64LittleEndian(gps.AsSpan(3), 900000000); input.Write(gps);
        BinaryPrimitives.WriteUInt64LittleEndian(imu.AsSpan(3), 901000000); input.Write(imu);
        input.Position = 0;
        using var output = new MemoryStream();
        var result = await Create().ExportAsync(input, ".bin", output, TestContext.Current.CancellationToken);
        Assert.Equal(6, result.Rows);
        var csv = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("1.000000,S1,IMU,AccX,1.25", csv);
        Assert.DoesNotContain("998877", csv); Assert.DoesNotContain("55.5", csv);
        Assert.DoesNotContain("GPS", csv); Assert.DoesNotContain("900000000", csv);
    }

}

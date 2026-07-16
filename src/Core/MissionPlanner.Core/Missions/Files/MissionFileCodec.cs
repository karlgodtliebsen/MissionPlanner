using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions.Files;

/// <inheritdoc />
public sealed class MissionFileCodec(IMissionProtocolMapper protocolMapper) : IMissionFileCodec
{
    private const string QgcWplHeader = "QGC WPL 110";
    private const int MissionJsonVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Param4 (yaw/heading) uses NaN for "not set" per MAVLink convention.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    /// <inheritdoc />
    public string Build(Mission mission, GeoPosition? home, MissionFileFormat format)
    {
        return format switch
        {
            MissionFileFormat.MissionJson => BuildMissionJson(mission, home),
            var _ => BuildQgcWpl(mission, home)
        };
    }

    /// <inheritdoc />
    public MissionFileContent Parse(string content)
    {
        var trimmed = content.TrimStart('﻿', ' ', '\t', '\r', '\n');

        if (trimmed.StartsWith("QGC WPL", StringComparison.OrdinalIgnoreCase))
        {
            return ParseQgcWpl(trimmed);
        }

        if (trimmed.StartsWith('{'))
        {
            return ParseMissionJson(trimmed);
        }

        throw new InvalidDataException("Unsupported mission file: expected a QGC WPL waypoint file or a JSON .mission document.");
    }

    private string BuildQgcWpl(Mission mission, GeoPosition? home)
    {
        var builder = new StringBuilder();
        builder.AppendLine(QgcWplHeader);

        var homePosition = ResolveHome(mission, home);
        AppendWplLine(builder, 0, 1, 0, (ushort)MissionCommand.NavigateWaypoint,
            0, 0, 0, 0, homePosition.LatitudeDegrees, homePosition.LongitudeDegrees, 0, 1);

        foreach (var item in mission.Items)
        {
            var p = protocolMapper.ToProtocol(item, mission.Type);
            AppendWplLine(builder, p.Sequence + 1, 0, p.Frame, p.Command,
                p.Param1, p.Param2, p.Param3, p.Param4, p.X / 1e7, p.Y / 1e7, p.Z, p.AutoContinue ? 1 : 0);
        }

        return builder.ToString();
    }

    private MissionFileContent ParseQgcWpl(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<MissionItem> items = [];
        GeoPosition? home = null;
        var skipped = 0;

        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 12)
            {
                continue;
            }

            var sequence = ushort.Parse(fields[0], CultureInfo.InvariantCulture);
            var latitude = double.Parse(fields[8], CultureInfo.InvariantCulture);
            var longitude = double.Parse(fields[9], CultureInfo.InvariantCulture);

            // Line 0 is the home position by QGC WPL convention.
            if (sequence == 0)
            {
                home = latitude != 0 || longitude != 0 ? new GeoPosition(latitude, longitude) : null;
                continue;
            }

            var protocolItem = new MavLinkMissionItem(
                (ushort)(sequence - 1),
                byte.Parse(fields[2], CultureInfo.InvariantCulture),
                ushort.Parse(fields[3], CultureInfo.InvariantCulture),
                false,
                fields[11] != "0",
                float.Parse(fields[4], CultureInfo.InvariantCulture),
                float.Parse(fields[5], CultureInfo.InvariantCulture),
                float.Parse(fields[6], CultureInfo.InvariantCulture),
                float.Parse(fields[7], CultureInfo.InvariantCulture),
                (int)Math.Round(latitude * 1e7),
                (int)Math.Round(longitude * 1e7),
                float.Parse(fields[10], CultureInfo.InvariantCulture),
                MavMissionType.Mission);

            AddOrSkip(items, protocolItem, ref skipped);
        }

        return new MissionFileContent(items, home, skipped);
    }

    private string BuildMissionJson(Mission mission, GeoPosition? home)
    {
        var document = new MissionDocument(
            MissionJsonVersion,
            mission.Name,
            mission.Type.ToString(),
            home is { } h ? new HomeDocument(h.LatitudeDegrees, h.LongitudeDegrees, 0) : null,
            mission.Items.Select(item =>
            {
                var p = protocolMapper.ToProtocol(item, mission.Type);
                return new ItemDocument(
                    p.Sequence,
                    p.Command,
                    item.Command.ToString(),
                    p.Frame,
                    p.AutoContinue,
                    p.Param1, p.Param2, p.Param3, p.Param4,
                    p.X / 1e7, p.Y / 1e7, p.Z);
            }).ToList());

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private MissionFileContent ParseMissionJson(string content)
    {
        var document = JsonSerializer.Deserialize<MissionDocument>(content, JsonOptions)
                       ?? throw new InvalidDataException("Empty JSON mission document.");

        List<MissionItem> items = [];
        var skipped = 0;

        foreach (var item in document.Items ?? [])
        {
            var protocolItem = new MavLinkMissionItem(
                (ushort)item.Sequence,
                item.Frame,
                item.Command,
                false,
                item.AutoContinue,
                item.Param1, item.Param2, item.Param3, item.Param4,
                (int)Math.Round(item.Latitude * 1e7),
                (int)Math.Round(item.Longitude * 1e7),
                item.AltitudeMeters,
                MavMissionType.Mission);

            AddOrSkip(items, protocolItem, ref skipped);
        }

        GeoPosition? home = document.Home is { } h && (h.Latitude != 0 || h.Longitude != 0)
            ? new GeoPosition(h.Latitude, h.Longitude)
            : null;

        return new MissionFileContent(items, home, skipped, document.Name);
    }

    private void AddOrSkip(List<MissionItem> items, MavLinkMissionItem protocolItem, ref int skipped)
    {
        try
        {
            items.Add(protocolMapper.FromProtocol(protocolItem));
        }
        catch (NotSupportedException)
        {
            skipped++;
        }
    }

    private static GeoPosition ResolveHome(Mission mission, GeoPosition? home)
    {
        return home
               ?? mission.Items.Select(PositionOf).FirstOrDefault(p => p is not null)
               ?? new GeoPosition(0, 0);
    }

    private static GeoPosition? PositionOf(MissionItem item)
    {
        return item switch
        {
            WaypointMissionItem x => x.Position,
            LandMissionItem x => x.Position,
            LoiterMissionItem x => x.Position,
            TakeoffMissionItem x => x.Position,
            var _ => null
        };
    }

    private static void AppendWplLine(StringBuilder builder, int sequence, int current, byte frame, ushort command,
        float p1, float p2, float p3, float p4, double latitude, double longitude, float altitude, int autoContinue)
    {
        builder.AppendLine(string.Join('\t',
            sequence.ToString(CultureInfo.InvariantCulture),
            current.ToString(CultureInfo.InvariantCulture),
            frame.ToString(CultureInfo.InvariantCulture),
            command.ToString(CultureInfo.InvariantCulture),
            p1.ToString("0.########", CultureInfo.InvariantCulture),
            p2.ToString("0.########", CultureInfo.InvariantCulture),
            p3.ToString("0.########", CultureInfo.InvariantCulture),
            p4.ToString("0.########", CultureInfo.InvariantCulture),
            latitude.ToString("0.########", CultureInfo.InvariantCulture),
            longitude.ToString("0.########", CultureInfo.InvariantCulture),
            altitude.ToString("0.######", CultureInfo.InvariantCulture),
            autoContinue.ToString(CultureInfo.InvariantCulture)));
    }

    private sealed record MissionDocument(
        int Version,
        string? Name,
        string? Type,
        HomeDocument? Home,
        List<ItemDocument>? Items);

    private sealed record HomeDocument(double Latitude, double Longitude, double AltitudeMeters);

    private sealed record ItemDocument(
        int Sequence,
        ushort Command,
        string? CommandName,
        byte Frame,
        bool AutoContinue,
        float Param1,
        float Param2,
        float Param3,
        float Param4,
        double Latitude,
        double Longitude,
        float AltitudeMeters);
}

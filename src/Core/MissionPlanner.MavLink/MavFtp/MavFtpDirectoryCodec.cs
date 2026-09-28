namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpDirectoryCodec.
/// </summary>
public static class MavFtpDirectoryCodec
{
    /// <summary>
    /// Provides the public API for Decode.
    /// </summary>
    public static IReadOnlyList<MavFtpDirectoryEntry> Decode(ReadOnlySpan<byte> data, bool withTime = false)
    {
        var entries = new List<MavFtpDirectoryEntry>();
        var start = 0;
        while (start < data.Length)
        {
            var end = data[start..].IndexOf((byte)0);
            if (end < 0)
            {
                end = data.Length - start;
            }

            var entry = data.Slice(start, end);
            start += end + 1;
            if (entry.IsEmpty)
            {
                continue;
            }

            var text = System.Text.Encoding.UTF8.GetString(entry[1..]);
            DateTimeOffset? modified = null;
            if (withTime && entry[0] is (byte)'F' or (byte)'D')
            {
                var fields = text.Split('\t');
                if (fields.Length != 3)
                {
                    throw new MavFtpProtocolException("Malformed timestamped MAVFTP directory entry.");
                }
                if (uint.TryParse(fields[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
                {
                    modified = DateTimeOffset.FromUnixTimeSeconds(seconds);
                }
                text = entry[0] == (byte)'F' ? $"{fields[0]}\t{fields[1]}" : fields[0];
            }
            switch ((char)entry[0])
            {
                case 'F':
                    var separator = text.LastIndexOf('\t');
                    if (separator <= 0 || !long.TryParse(text[(separator + 1)..], out var size) || size < 0)
                    {
                        throw new MavFtpProtocolException("Malformed MAVFTP file directory entry.");
                    }

                    entries.Add(new MavFtpDirectoryEntry(text[..separator], MavFtpDirectoryEntryType.File, size) { ModifiedUtc = modified });
                    break;
                case 'D': entries.Add(new MavFtpDirectoryEntry(text, MavFtpDirectoryEntryType.Directory, null) { ModifiedUtc = modified }); break;
                case 'S': entries.Add(new MavFtpDirectoryEntry(text, MavFtpDirectoryEntryType.Skip, null)); break;
                default: throw new MavFtpProtocolException("Unknown MAVFTP directory entry type.");
            }
        }

        return entries;
    }
}

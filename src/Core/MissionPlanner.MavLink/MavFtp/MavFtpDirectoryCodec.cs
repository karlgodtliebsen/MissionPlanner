namespace MissionPlanner.MavLink.MavFtp;

public static class MavFtpDirectoryCodec
{
    public static IReadOnlyList<MavFtpDirectoryEntry> Decode(ReadOnlySpan<byte> data)
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
            switch ((char)entry[0])
            {
                case 'F':
                    var separator = text.LastIndexOf('\t');
                    if (separator <= 0 || !long.TryParse(text[(separator + 1)..], out var size))
                    {
                        throw new MavFtpProtocolException("Malformed MAVFTP file directory entry.");
                    }

                    entries.Add(new MavFtpDirectoryEntry(text[..separator], MavFtpDirectoryEntryType.File, size));
                    break;
                case 'D': entries.Add(new MavFtpDirectoryEntry(text, MavFtpDirectoryEntryType.Directory, null)); break;
                case 'S': entries.Add(new MavFtpDirectoryEntry(text, MavFtpDirectoryEntryType.Skip, null)); break;
                default: throw new MavFtpProtocolException("Unknown MAVFTP directory entry type.");
            }
        }

        return entries;
    }
}

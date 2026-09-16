using System.Globalization;
using MissionPlanner.Core.ConfigTuning;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Groups actual ArduPilot SERIAL parameters without implying board-specific UART wiring.</summary>
/// <param name="Index">The ArduPilot serial index.</param>
/// <param name="Protocol">The protocol parameter when reported.</param>
/// <param name="Baud">The configured speed parameter when reported.</param>
/// <param name="Options">The options parameter when reported.</param>
public sealed record SerialPortConfiguration(int Index, string? Protocol, string? Baud, string? Options)
{
    /// <summary>Gets the reported editable names.</summary>
    public IReadOnlyList<string> Names => new[] { Protocol, Baud, Options }.OfType<string>().ToArray();

    /// <summary>Discovers sparse, numerically ordered groups, including future SERIALn suffixes.</summary>
    public static IReadOnlyList<SerialPortConfiguration> Discover(IEnumerable<string> names)
    {
        var groups = new SortedDictionary<int, HashSet<string>>();
        foreach (var name in names)
        {
            if (!TryParseIndex(name, out var index))
            {
                continue;
            }
            if (!groups.TryGetValue(index, out var group))
            {
                group = new HashSet<string>(StringComparer.Ordinal);
                groups.Add(index, group);
            }
            group.Add(name);
        }
        return groups.Select(pair =>
        {
            string? Find(string suffix)
            {
                var name = $"SERIAL{pair.Key}_{suffix}";
                return pair.Value.Contains(name) ? name : null;
            }
            return new SerialPortConfiguration(pair.Key, Find("PROTOCOL"), Find("BAUD"), Find("OPTIONS"));
        }).ToArray();
    }

    /// <summary>Parses only canonical numeric SERIALn groups and tolerates unknown future suffixes.</summary>
    public static bool TryParseIndex(string name, out int index)
    {
        index = 0;
        if (!name.StartsWith("SERIAL", StringComparison.Ordinal))
        {
            return false;
        }
        var separator = name.IndexOf('_', 6);
        if (separator <= 6 || separator == name.Length - 1)
        {
            return false;
        }
        var digits = name.AsSpan(6, separator - 6);
        return !(digits.Length > 1 && digits[0] == '0') &&
            digits.IndexOfAnyExceptInRange('0', '9') < 0 &&
            int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    /// <summary>Warns only about metadata-identified RC input duplication; no protocol numbers are assumed.</summary>
    public static string Diagnose(IEnumerable<ParameterEditField> fields)
    {
        var all = fields.ToArray();
        var receivers = all.Where(field => TryParseIndex(field.Name, out _) &&
            field.Name.EndsWith("_PROTOCOL", StringComparison.Ordinal) &&
            field.Metadata.Options.Any(option => option.Value == field.PendingValue &&
                (string.Equals(option.Label.Trim(), "RCIN", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(option.Label.Trim(), "RC Input", StringComparison.OrdinalIgnoreCase)))).ToArray();
        if (receivers.Length < 2)
        {
            return string.Empty;
        }
        var options = all.FirstOrDefault(field => field.Name == "RC_OPTIONS");
        var support = options?.Metadata.Bitmask.FirstOrDefault(bit =>
            bit.Bit is >= 0 and < 32 &&
            bit.Label.Contains("multiple", StringComparison.OrdinalIgnoreCase) &&
            bit.Label.Contains("receiver", StringComparison.OrdinalIgnoreCase));
        var disabled = support is not null && options!.PendingValue >= 0 &&
            (((ulong)options.PendingValue & (1UL << support.Bit)) == 0);
        return disabled
            ? "Multiple RCIN ports are configured, but multiple-receiver support is not enabled."
            : "Multiple serial ports are configured for RC input. This can be intentional; review the receiver assignments.";
    }
}

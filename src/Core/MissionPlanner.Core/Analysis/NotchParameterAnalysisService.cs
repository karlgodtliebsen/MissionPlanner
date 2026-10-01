using System.Collections.Immutable;
using System.Globalization;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Analysis;

/// <summary>Reads existing parameter snapshots and metadata for analysis only.</summary>
public sealed class NotchParameterAnalysisService(IActiveVehicleContext active, IVehicleParameterRegistry registry,
    IVehicleParameterMetadataService metadata)
{
    /// <summary>Supported firmware-family names for saved metadata lookup.</summary>
    public static IReadOnlyList<string> VehicleFamilies { get; } = Array.AsReadOnly(Enum.GetNames<VehicleType>());

    /// <summary>Captures the current connected parameter registry without requesting or writing parameters.</summary>
    /// <param name="token">Operation cancellation.</param>
    /// <returns>A snapshot bound to the captured vehicle identity.</returns>
    public async Task<NotchParameterSnapshot> ReadConnectedAsync(CancellationToken token)
    {
        var vehicle = active.Current;
        if (!vehicle.IsOnline || vehicle.VehicleId is not { } id)
        {
            throw new InvalidOperationException("Connect a vehicle or load a saved parameter snapshot.");
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, active.ConnectionCancellationToken);
        var values = registry.GetAllParameters(id).ToDictionary(p => p.Key, p => (double)p.Value.Value, StringComparer.Ordinal);
        var definitions = await metadata.GetAllMetadataAsync(id, linked.Token).ConfigureAwait(false);
        linked.Token.ThrowIfCancellationRequested();
        return active.VehicleId != id
            ? throw new OperationCanceledException("Vehicle changed during parameter capture.")
            : Build($"Connected snapshot: {vehicle.DisplayName}", values, definitions);
    }

    /// <summary>Reads invariant name/value parameter text and enriches it with the selected firmware metadata.</summary>
    /// <param name="stream">Existing .param/.params/.csv/.txt data; caller owns the stream.</param>
    /// <param name="name">File name.</param>
    /// <param name="vehicleFamily">Explicit firmware family for metadata lookup.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Read-only snapshot; unknown metadata is explicitly reported.</returns>
    public async Task<NotchParameterSnapshot> ReadSavedAsync(Stream stream, string name, string vehicleFamily, CancellationToken token)
    {
        if (!Enum.TryParse<VehicleType>(vehicleFamily, out var family) || !Enum.IsDefined(family))
        {
            throw new ArgumentException("Select the saved vehicle's firmware family.", nameof(vehicleFamily));
        }
        using var reader = new StreamReader(stream, leaveOpen: true);
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        var count = 0;
        while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
        {
            if (++count > 100000 || line.Length > 4096)
            {
                throw new InvalidDataException("Parameter file exceeds import limits.");
            }
            line = line.Split("//", 2)[0].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            var parts = line.Split([',', '=', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            {
                throw new InvalidDataException($"Invalid parameter name/value on line {count}.");
            }
            values[parts[0]] = value;
        }
        var definitions = await metadata.GetAllMetadataAsync(family, token).ConfigureAwait(false);
        return Build($"Saved snapshot: {name} ({vehicleFamily})", values, definitions);
    }

    /// <summary>Resolves supported static notches from actual values, keeping metadata and uncertainty.</summary>
    /// <param name="source">Snapshot provenance.</param>
    /// <param name="values">Existing parameter model values.</param>
    /// <param name="definitions">Existing firmware metadata; never fabricated.</param>
    /// <returns>Static models and explicit limitations.</returns>
    public static NotchParameterSnapshot Build(string source, IReadOnlyDictionary<string, double> values,
        IReadOnlyDictionary<string, ParameterMetadata> definitions)
    {
        var relevant = values.Where(p => p.Key.StartsWith("INS_HNTCH_", StringComparison.Ordinal) || p.Key.StartsWith("INS_HNTC2_", StringComparison.Ordinal))
            .OrderBy(p => p.Key).Select(p => new NotchParameterValue(p.Key, p.Value,
                definitions.GetValueOrDefault(p.Key)?.Description ?? "Firmware metadata unavailable.")).ToImmutableArray();
        var filters = ImmutableArray.CreateBuilder<NotchFilter>();
        var limitations = ImmutableArray.CreateBuilder<string>();
        foreach (var prefix in new[] { "INS_HNTCH", "INS_HNTC2" })
        {
            double Value(string suffix)
            {
                return values.GetValueOrDefault(prefix + "_" + suffix, double.NaN);
            }

            if (!values.ContainsKey(prefix + "_ENABLE"))
            {
                limitations.Add($"{prefix}: enable state unavailable.");
                continue;
            }
            if (Value("ENABLE") == 0)
            {
                continue;
            }
            if (Value("ENABLE") != 1)
            {
                limitations.Add($"{prefix}: unsupported enable value.");
                continue;
            }
            if (Value("MODE") != 0 || Value("OPTS") != 0)
            {
                limitations.Add($"{prefix}: dynamic tracking, options, or missing MODE/OPTS prevent a static coverage claim.");
                continue;
            }
            var frequency = Value("FREQ");
            var bandwidth = Value("BW");
            var attenuation = Value("ATT");
            var mask = Value("HMNCS");
            if (!double.IsFinite(frequency) || frequency <= 0 || !double.IsFinite(bandwidth) || bandwidth <= 0 || bandwidth > frequency ||
                !double.IsFinite(attenuation) || attenuation < 0 || attenuation > 120 || !double.IsFinite(mask) || mask < 0 || mask > 65535 || mask != Math.Truncate(mask))
            {
                limitations.Add($"{prefix}: incomplete or invalid static configuration.");
                continue;
            }
            for (var order = 1; order <= 16; order++)
            {
                if (((int)mask & (1 << (order - 1))) != 0)
                {
                    filters.Add(new NotchFilter(frequency * order, bandwidth * order, attenuation));
                }
            }
        }
        limitations.Add("Bands are nominal center ± bandwidth/2. Simulation uses a generic static biquad model, not firmware-exact attenuation or dynamic tracking.");
        return new NotchParameterSnapshot(source, relevant, filters.ToImmutable(), limitations.ToImmutable());
    }
}

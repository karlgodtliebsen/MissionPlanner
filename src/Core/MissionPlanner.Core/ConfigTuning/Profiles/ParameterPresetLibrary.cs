using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>A folder or immutable Git blob in ArduPilot's official frame-parameter library.</summary>
public sealed record ParameterPresetEntry(string Name, string Path, string Sha, bool IsDirectory);

/// <summary>Browses the official library on demand and imports bounded, validated values as a local review profile.</summary>
public sealed class ParameterPresetLibrary(IHttpClientFactory clients)
{
    private const string Root = "Tools/Frame_params";

    /// <summary>Lists one directory; navigation never loads or applies parameter values.</summary>
    public async Task<IReadOnlyList<ParameterPresetEntry>> ListAsync(string directory, CancellationToken token)
    {
        ValidatePath(directory);
        using var document = await GetAsync("contents/" + Escape(directory) + "?ref=master", 2 * 1024 * 1024, token).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("The library response is not a directory.");
        var entries = new List<ParameterPresetEntry>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var path = item.GetProperty("path").GetString() ?? "";
            ValidatePath(path);
            var folder = item.GetProperty("type").GetString() == "dir";
            if (!folder && !path.EndsWith(".param", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".parm", StringComparison.OrdinalIgnoreCase)) continue;
            if (item.TryGetProperty("size", out var size) && size.GetInt64() > 512 * 1024) continue;
            var sha = item.GetProperty("sha").GetString() ?? "";
            ValidateSha(sha);
            entries.Add(new(item.GetProperty("name").GetString() ?? System.IO.Path.GetFileName(path), path, sha, folder));
            if (entries.Count > 1000) throw new InvalidDataException("The library directory exceeds 1000 entries.");
        }
        return entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Downloads the selected immutable blob, verifies its identity and returns an unapplied profile.</summary>
    public async Task<ParameterProfile> DownloadAsync(ParameterPresetEntry entry, CancellationToken token)
    {
        ValidatePath(entry.Path);
        ValidateSha(entry.Sha);
        if (entry.IsDirectory) throw new ArgumentException("Select a parameter file.");
        using var document = await GetAsync("git/blobs/" + entry.Sha, 1024 * 1024, token).ConfigureAwait(false);
        if (document.RootElement.GetProperty("encoding").GetString() != "base64") throw new InvalidDataException("Unsupported library encoding.");
        var bytes = Convert.FromBase64String(document.RootElement.GetProperty("content").GetString() ?? "");
        if (bytes.Length > 512 * 1024) throw new InvalidDataException("Parameter preset exceeds 512 KiB.");
        var header = Encoding.ASCII.GetBytes("blob " + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\0");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1); // Git content identity, not an authentication mechanism.
        hash.AppendData(header); hash.AppendData(bytes);
        if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(entry.Sha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The preset does not match the selected Git revision.");
        var values = Parse(new UTF8Encoding(false, true).GetString(bytes));
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), entry.Name, "Official ArduPilot frame preset. Firmware and hardware compatibility are unspecified; review every selected value. Imported Git blob: " + entry.Sha,
            now, now, null, null, null, "https://github.com/ArduPilot/ardupilot/blob/master/" + Escape(entry.Path) + " (blob " + entry.Sha + ")", values, ["official-preset"]);
    }

    /// <summary>Parses two-column ArduPilot .param/.parm text with explicit duplicate and finite-value checks.</summary>
    public static IReadOnlyDictionary<string, double> Parse(string text)
    {
        if (text.Length > 512 * 1024) throw new InvalidDataException("Parameter preset exceeds the input limit.");
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        using var lines = new StringReader(text);
        var number = 0;
        while (lines.ReadLine() is { } line)
        {
            number++;
            line = line.Split('#', 2)[0].Trim();
            if (line.Length == 0) continue;
            var parts = line.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[0].Length is < 1 or > 16 || parts[0].Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '_')) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || Math.Abs(value) > float.MaxValue ||
                !values.TryAdd(parts[0], value)) throw new InvalidDataException($"Invalid or duplicate preset entry on line {number}.");
            if (values.Count > 10000) throw new InvalidDataException("Preset exceeds 10000 parameters.");
        }
        if (values.Count == 0) throw new InvalidDataException("The preset contains no values.");
        return values;
    }

    private async Task<JsonDocument> GetAsync(string relative, int limit, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/ArduPilot/ardupilot/" + relative);
        request.Headers.UserAgent.ParseAdd("MissionPlannerNextGen/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var client = clients.CreateClient("ParameterPresets");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Library response exceeds the size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + count > limit) throw new InvalidDataException("Library response exceeds the size limit.");
            buffer.Write(chunk, 0, count);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }

    private static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    private static void ValidateSha(string sha)
    {
        if (sha.Length != 40 || !sha.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid Git blob identity.");
    }
    private static void ValidatePath(string path)
    {
        if (!(path == Root || path.StartsWith(Root + "/", StringComparison.Ordinal)) || path.Length > 512 ||
            path.Contains('\\') || path.Any(char.IsControl) || path.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("The selected path is outside the official frame-parameter library.");
    }
}

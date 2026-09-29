using System.Text.Json;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;

/// <summary>Cached per-user vehicle descriptions with atomic disk persistence where supported.</summary>
public sealed class JsonVehicleLocalDetailsStore : IVehicleLocalDetailsStore
{
    private readonly Lock sync = new();
    private readonly string? path;
    private readonly Dictionary<string, VehicleLocalDetails> values = new();
    private bool unreadable;

    /// <summary>Uses local application data, or session memory on platforms without a filesystem.</summary>
    public JsonVehicleLocalDetailsStore() : this(OperatingSystem.IsBrowser() ||
        string.IsNullOrWhiteSpace(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MissionPlanner Next Gen", "vehicle-details.json"))
    {
    }

    /// <summary>Creates a store at an explicit path; null selects memory-only storage.</summary>
    public JsonVehicleLocalDetailsStore(string? path)
    {
        this.path = path;
        if (path is null)
        {
            return;
        }
        try
        {
            if (File.Exists(path))
            {
                values = JsonSerializer.Deserialize<Dictionary<string, VehicleLocalDetails>>(File.ReadAllText(path)) ?? new();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            unreadable = true;
        }
    }

    /// <inheritdoc />
    public VehicleLocalDetails? Get(string key)
    {
        lock (sync)
        {
            return values.GetValueOrDefault(key);
        }
    }

    /// <inheritdoc />
    public string Save(string key, VehicleLocalDetails details)
    {
        lock (sync)
        {
            values[key] = details;
            if (path is null)
            {
                return "Saved for this app session; persistent local storage is unavailable on this platform.";
            }
            if (unreadable)
            {
                return "Saved for this app session only. The existing local details file could not be read and has been preserved.";
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(values));
                File.Move(temporary, path, overwrite: true);
                return "Vehicle details saved locally for future connections.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return "Saved for this app session only; persistent local storage is unavailable.";
            }
        }
    }
}

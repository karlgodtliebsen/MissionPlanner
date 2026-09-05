using System.Text.Json;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;

/// <summary>
/// Persists setup-completion evidence in the user's local application-data folder.
/// </summary>
public sealed class JsonSetupCompletionStore : ISetupCompletionStore
{
    private readonly Lock sync = new();
    private readonly string filePath;

    /// <summary>Creates a store using the default per-user application-data location.</summary>
    public JsonSetupCompletionStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MissionPlanner Next Gen",
            "setup-completion-evidence.json"))
    {
    }

    internal JsonSetupCompletionStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    /// <inheritdoc />
    public IReadOnlyList<SetupCompletionEvidence> GetAll()
    {
        lock (sync)
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                return JsonSerializer.Deserialize<List<SetupCompletionEvidence>>(stream) ?? [];
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }

    /// <inheritdoc />
    public void Save(SetupCompletionEvidence evidence)
    {
        lock (sync)
        {
            var values = GetAll().Where(item => item.VehicleKey != evidence.VehicleKey || item.Workflow != evidence.Workflow).ToList();
            values.Add(evidence);
            Write(values);
        }
    }

    /// <inheritdoc />
    public void Remove(string vehicleKey, SetupWorkflowKey workflow)
    {
        lock (sync)
        {
            var values = GetAll().Where(item => item.VehicleKey != vehicleKey || item.Workflow != workflow).ToList();
            Write(values);
        }
    }

    private void Write(IReadOnlyCollection<SetupCompletionEvidence> values)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The setup-completion path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = filePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, values);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

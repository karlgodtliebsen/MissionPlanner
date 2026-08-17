using System.Text.Json;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Setup.Definitions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;

/// <summary>
/// Persists setup-completion evidence in the application's local preferences.
/// </summary>
public sealed class PreferencesSetupCompletionStore : ISetupCompletionStore
{
    private const string PreferenceKey = "MissionPlanner.Setup.CompletionEvidence.v1";
    private readonly Lock sync = new();

    /// <inheritdoc />
    public IReadOnlyList<SetupCompletionEvidence> GetAll()
    {
        lock (sync)
        {
            var value = Microsoft.Maui.Storage.Preferences.Default.Get(PreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<SetupCompletionEvidence>>(value) ?? [];
            }
            catch (JsonException)
            {
                Microsoft.Maui.Storage.Preferences.Default.Remove(PreferenceKey);
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
            Microsoft.Maui.Storage.Preferences.Default.Set(PreferenceKey, JsonSerializer.Serialize(values));
        }
    }

    /// <inheritdoc />
    public void Remove(string vehicleKey, SetupWorkflowKey workflow)
    {
        lock (sync)
        {
            var values = GetAll().Where(item => item.VehicleKey != vehicleKey || item.Workflow != workflow).ToList();
            Microsoft.Maui.Storage.Preferences.Default.Set(PreferenceKey, JsonSerializer.Serialize(values));
        }
    }
}

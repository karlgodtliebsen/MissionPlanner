using System.Text.Json;
using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>Persists setup-completion evidence in the application's local preferences.</summary>
public sealed class PreferencesSetupCompletionStore : ISetupCompletionStore
{
    private const string PreferenceKey = "MissionPlanner.Setup.CompletionEvidence.v1";
    private readonly object sync = new();

    /// <inheritdoc />
    public IReadOnlyList<SetupCompletionEvidence> GetAll()
    {
        lock (sync)
        {
            var value = Preferences.Default.Get(PreferenceKey, string.Empty);
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
                Preferences.Default.Remove(PreferenceKey);
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
            Preferences.Default.Set(PreferenceKey, JsonSerializer.Serialize(values));
        }
    }

    /// <inheritdoc />
    public void Remove(string vehicleKey, SetupWorkflowKey workflow)
    {
        lock (sync)
        {
            var values = GetAll().Where(item => item.VehicleKey != vehicleKey || item.Workflow != workflow).ToList();
            Preferences.Default.Set(PreferenceKey, JsonSerializer.Serialize(values));
        }
    }
}

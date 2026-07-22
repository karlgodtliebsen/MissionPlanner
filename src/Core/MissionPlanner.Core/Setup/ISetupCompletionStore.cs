namespace MissionPlanner.Core.Setup;

/// <summary>Stores local setup-completion evidence without treating it as vehicle truth.</summary>
public interface ISetupCompletionStore
{
    /// <summary>Gets all locally stored completion evidence.</summary>
    /// <returns>A snapshot of the stored evidence.</returns>
    IReadOnlyList<SetupCompletionEvidence> GetAll();

    /// <summary>Adds or replaces evidence for one vehicle workflow.</summary>
    /// <param name="evidence">The evidence to store.</param>
    void Save(SetupCompletionEvidence evidence);

    /// <summary>Removes evidence for one vehicle workflow.</summary>
    /// <param name="vehicleKey">The local vehicle identity key.</param>
    /// <param name="workflow">The workflow key.</param>
    void Remove(string vehicleKey, SetupWorkflowKey workflow);
}

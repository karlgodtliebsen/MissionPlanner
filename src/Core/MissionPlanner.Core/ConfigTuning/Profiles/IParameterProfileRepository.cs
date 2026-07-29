namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>Replaceable persistence for named parameter profiles.</summary>
public interface IParameterProfileRepository
{
    /// <summary>Lists stored profiles.</summary>
    Task<IReadOnlyList<ParameterProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a profile by ID.</summary>
    Task<ParameterProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Atomically creates or replaces a profile.</summary>
    Task SaveAsync(ParameterProfile profile, CancellationToken cancellationToken = default);
    /// <summary>Deletes a profile.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Renames an existing profile.</summary>
    Task<ParameterProfile> RenameAsync(Guid id, string name, CancellationToken cancellationToken = default);
    /// <summary>Duplicates an existing profile under a new ID and name.</summary>
    Task<ParameterProfile> DuplicateAsync(Guid id, string name, CancellationToken cancellationToken = default);
    /// <summary>Imports and stores a profile document.</summary>
    Task<ParameterProfile> ImportAsync(Stream source, CancellationToken cancellationToken = default);
    /// <summary>Exports a profile document.</summary>
    Task ExportAsync(Guid id, Stream destination, CancellationToken cancellationToken = default);
}

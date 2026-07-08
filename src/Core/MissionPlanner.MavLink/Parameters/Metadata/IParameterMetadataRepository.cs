namespace MissionPlanner.MavLink.Parameters.Metadata;

/// <summary>
/// Repository for parameter metadata with caching support.
/// </summary>
public interface IParameterMetadataRepository
{
    /// <summary>
    /// Gets metadata for a specific parameter and vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parameter metadata, or null if not found.</returns>
    Task<ParameterMetadata?> GetMetadataAsync(
        VehicleType vehicleType,
        string parameterName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metadata for a vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of parameter name to metadata.</returns>
    Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(
        VehicleType vehicleType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the metadata cache for a vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshAsync(VehicleType vehicleType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached metadata.
    /// </summary>
    void ClearCache();
}

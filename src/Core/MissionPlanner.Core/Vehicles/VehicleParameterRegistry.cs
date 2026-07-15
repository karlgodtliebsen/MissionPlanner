using System.Collections.Concurrent;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Thread-safe registry for storing and retrieving vehicle parameters.
/// </summary>
public sealed class VehicleParameterRegistry : IVehicleParameterRegistry
{
    private readonly ConcurrentDictionary<VehicleId, VehicleParameterCollection> parametersByVehicle = new();

    /// <inheritdoc/>
    public void StoreParameter(VehicleId vehicleId, VehicleParameter parameter, CancellationToken cancellationToken)
    {
        var collection = parametersByVehicle.GetOrAdd(vehicleId, _ => new VehicleParameterCollection());
        collection.AddOrUpdate(parameter);
        //ParameterReceived?.Invoke(vehicleId, parameter, cancellationToken);
    }

    /// <inheritdoc/>
    public VehicleParameter? GetParameter(VehicleId vehicleId, string parameterName)
    {
        return parametersByVehicle.TryGetValue(vehicleId, out var collection) ? collection.GetParameter(parameterName) : null;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, VehicleParameter> GetAllParameters(VehicleId vehicleId)
    {
        return parametersByVehicle.TryGetValue(vehicleId, out var collection)
            ? collection.GetAll()
            : new Dictionary<string, VehicleParameter>();
    }

    /// <inheritdoc/>
    public ushort? GetParameterCount(VehicleId vehicleId)
    {
        return parametersByVehicle.TryGetValue(vehicleId, out var collection) ? collection.TotalCount : null;
    }

    /// <inheritdoc/>
    public void ClearParameters(VehicleId vehicleId)
    {
        parametersByVehicle.TryRemove(vehicleId, out var _);
    }

    private sealed class VehicleParameterCollection
    {
        private readonly ConcurrentDictionary<string, VehicleParameter> parameters = new();
        private ushort? totalCount;

        public ushort? TotalCount => totalCount;

        public void AddOrUpdate(VehicleParameter parameter)
        {
            parameters.AddOrUpdate(parameter.Name, parameter, (_, _) => parameter);

            // Update total count from the parameter if available
            if (parameter.Count > 0)
            {
                totalCount = parameter.Count;
            }
        }

        public VehicleParameter? GetParameter(string name)
        {
            parameters.TryGetValue(name, out var parameter);
            return parameter;
        }

        public IReadOnlyDictionary<string, VehicleParameter> GetAll()
        {
            return parameters;
        }
    }
}

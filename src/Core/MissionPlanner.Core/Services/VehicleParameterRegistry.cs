using System.Collections.Concurrent;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Thread-safe registry for storing and retrieving vehicle parameters.
/// </summary>
public sealed class VehicleParameterRegistry : IVehicleParameterRegistry
{
    private readonly ConcurrentDictionary<VehicleId, VehicleParameterCollection> _parametersByVehicle = new();

    /// <inheritdoc/>
    public void StoreParameter(VehicleId vehicleId, VehicleParameter parameter)
    {
        var collection = _parametersByVehicle.GetOrAdd(vehicleId, _ => new VehicleParameterCollection());
        collection.AddOrUpdate(parameter);
    }

    /// <inheritdoc/>
    public VehicleParameter? GetParameter(VehicleId vehicleId, string parameterName)
    {
        if (_parametersByVehicle.TryGetValue(vehicleId, out var collection))
        {
            return collection.GetParameter(parameterName);
        }

        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, VehicleParameter> GetAllParameters(VehicleId vehicleId)
    {
        if (_parametersByVehicle.TryGetValue(vehicleId, out var collection))
        {
            return collection.GetAll();
        }

        return new Dictionary<string, VehicleParameter>();
    }

    /// <inheritdoc/>
    public ushort? GetParameterCount(VehicleId vehicleId)
    {
        if (_parametersByVehicle.TryGetValue(vehicleId, out var collection))
        {
            return collection.TotalCount;
        }

        return null;
    }

    /// <inheritdoc/>
    public void ClearParameters(VehicleId vehicleId)
    {
        _parametersByVehicle.TryRemove(vehicleId, out _);
    }

    private sealed class VehicleParameterCollection
    {
        private readonly ConcurrentDictionary<string, VehicleParameter> _parameters = new();
        private ushort? _totalCount;

        public ushort? TotalCount => _totalCount;

        public void AddOrUpdate(VehicleParameter parameter)
        {
            _parameters.AddOrUpdate(parameter.Name, parameter, (_, _) => parameter);

            // Update total count from the parameter if available
            if (parameter.Count > 0)
            {
                _totalCount = parameter.Count;
            }
        }

        public VehicleParameter? GetParameter(string name)
        {
            _parameters.TryGetValue(name, out var parameter);
            return parameter;
        }

        public IReadOnlyDictionary<string, VehicleParameter> GetAll()
        {
            return _parameters;
        }
    }
}

using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Simulation;

/// <summary>Allocates deterministic SITL identities, endpoints, locations, and artifact paths.</summary>
public sealed class SimulationFleetAllocator(
    IVehicleRegistry vehicleRegistry,
    IOptions<SimulationWorkspaceOptions> options) : ISimulationFleetAllocator
{
    /// <inheritdoc />
    public IReadOnlyList<SimulationInstanceAllocation> Allocate(
        SimulationFleetLaunchRequest request,
        IReadOnlyCollection<SimulationInstanceAllocation> occupied)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(occupied);
        ValidateRequest(request);

        var instances = occupied.Select(item => item.Profile.EffectiveLaunchSettings.Instance).ToHashSet();
        var systemIds = occupied.Select(item => item.Profile.EffectiveLaunchSettings.SystemId)
            .Concat(vehicleRegistry.Vehicles.Select(vehicle => vehicle.Id.SystemId))
            .ToHashSet();
        var endpoints = occupied.SelectMany(item => GetAllEndpoints(item.Profile)).Select(ToEndpointKey).ToHashSet();
        var allocations = new List<SimulationInstanceAllocation>(request.Count);
        for (var index = 0; index < request.Count; index++)
        {
            var baseSettings = request.BaseProfile.EffectiveLaunchSettings;
            var instance = checked(baseSettings.Instance + index);
            var systemIdValue = baseSettings.SystemId + index;
            if (instance > 254 || systemIdValue > byte.MaxValue)
            {
                throw new SimulationAllocationException("Fleet allocation exceeds the supported instance or MAVLink SystemId range.");
            }

            var systemId = checked((byte)systemIdValue);
            if (!instances.Add(instance))
            {
                throw new SimulationAllocationException($"SITL instance {instance} is already allocated.");
            }

            if (!systemIds.Add(systemId))
            {
                throw new SimulationAllocationException($"MAVLink SystemId {systemId} is already connected or allocated.");
            }

            var portOffset = checked(index * request.PortStride);
            var profileEndpoints = request.BaseProfile.Endpoints
                .Select(endpoint => endpoint with { Port = CheckedPort(endpoint.Port, portOffset) })
                .ToArray();
            var serialEndpoints = baseSettings.EffectiveSerialEndpoints
                .Select(endpoint => endpoint with { Port = CheckedPort(endpoint.Port, portOffset) })
                .ToArray();
            var settings = baseSettings with
            {
                Instance = instance,
                SystemId = systemId,
                AdditionalSerialEndpoints = serialEndpoints
            };
            var offset = request.Formation.Offsets[index];
            var profile = request.BaseProfile with
            {
                Id = CreateDeterministicId(request.BaseProfile.Id, index),
                Name = $"{request.BaseProfile.Name} #{index + 1}",
                Location = ApplyOffset(request.BaseProfile.Location, offset),
                Endpoints = profileEndpoints,
                LaunchSettings = settings
            };

            foreach (var endpoint in GetAllEndpoints(profile))
            {
                if (!endpoints.Add(ToEndpointKey(endpoint)))
                {
                    throw new SimulationAllocationException(
                        $"Endpoint collision for {endpoint.Transport.ToString().ToUpperInvariant()} {endpoint.Host}:{endpoint.Port}.");
                }
            }

            allocations.Add(new SimulationInstanceAllocation(
                CreateDeterministicId(request.BaseProfile.Id, index, "fleet-session"),
                index,
                profile,
                offset,
                SimulationInstanceArtifacts.Create(options.Value.LogRootDirectory, instance, systemId)));
        }

        return allocations;
    }

    private static void ValidateRequest(SimulationFleetLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.BaseProfile);
        ArgumentNullException.ThrowIfNull(request.Formation);
        if (request.Count is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Fleet count must be between 1 and 32.");
        }

        if (request.PortStride is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Port stride must be between 1 and 1000.");
        }

        if (request.MaximumConcurrency is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum concurrency must be between 1 and 16.");
        }

        if (request.Formation.Offsets.Count != request.Count)
        {
            throw new SimulationAllocationException(
                $"Formation '{request.Formation.Name}' contains {request.Formation.Offsets.Count} offsets for {request.Count} instances.");
        }

        if (request.Formation.Offsets.Any(offset =>
                !double.IsFinite(offset.NorthMeters) ||
                !double.IsFinite(offset.EastMeters) ||
                !double.IsFinite(offset.AltitudeMeters) ||
                !double.IsFinite(offset.HeadingDegrees)))
        {
            throw new SimulationAllocationException("Formation offsets must contain only finite values.");
        }
    }

    private static int CheckedPort(int port, int offset)
    {
        var result = checked(port + offset);
        if (result is < 1 or > 65535)
        {
            throw new SimulationAllocationException($"Allocated port {result} is outside the valid range.");
        }

        return result;
    }

    private static IEnumerable<SimulationEndpoint> GetAllEndpoints(SimulatorProfile profile)
    {
        foreach (var endpoint in profile.Endpoints)
        {
            yield return endpoint;
        }

        foreach (var endpoint in profile.EffectiveLaunchSettings.EffectiveSerialEndpoints)
        {
            yield return new SimulationEndpoint(
                $"Serial{endpoint.Index}",
                endpoint.Transport == ArduPilotSerialTransport.UdpClient
                    ? SimulationEndpointTransport.Udp
                    : SimulationEndpointTransport.Tcp,
                endpoint.Host,
                endpoint.Port);
        }
    }

    private static string ToEndpointKey(SimulationEndpoint endpoint) =>
        $"{endpoint.Transport}:{endpoint.Host.Trim().ToUpperInvariant()}:{endpoint.Port}";

    private static SimulationLocation ApplyOffset(SimulationLocation location, SimulationFormationOffset offset)
    {
        const double earthRadiusMeters = 6378137;
        var latitudeRadians = location.LatitudeDegrees * Math.PI / 180;
        var latitude = location.LatitudeDegrees + offset.NorthMeters / earthRadiusMeters * 180 / Math.PI;
        var longitudeScale = Math.Max(0.000001, Math.Abs(Math.Cos(latitudeRadians)));
        var longitude = location.LongitudeDegrees + offset.EastMeters / (earthRadiusMeters * longitudeScale) * 180 / Math.PI;
        var heading = (location.HeadingDegrees + offset.HeadingDegrees) % 360;
        if (heading < 0)
        {
            heading += 360;
        }

        return new SimulationLocation(
            latitude,
            longitude,
            location.AltitudeMeters + offset.AltitudeMeters,
            heading);
    }

    private static Guid CreateDeterministicId(Guid profileId, int index, string purpose = "profile")
    {
        var input = System.Text.Encoding.UTF8.GetBytes($"{profileId:N}:{purpose}:{index}");
        var hash = SHA256.HashData(input);
        return new Guid(hash.AsSpan(0, 16));
    }
}

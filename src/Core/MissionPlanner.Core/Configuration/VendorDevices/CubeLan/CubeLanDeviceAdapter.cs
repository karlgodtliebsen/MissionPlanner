using System.Text.Json;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Generated;

namespace MissionPlanner.Core.Configuration.VendorDevices.CubeLan;

/// <summary>Configures the verified CubeLAN 8-port switch subset through MAVLink DEVICE_OP.</summary>
public sealed class CubeLanDeviceAdapter : IVendorDeviceAdapter<CubeLanConfiguration>
{
    private const byte Bus = 0;
    private const byte Address = 0x50;
    private const byte ConfigurationLength = 100;
    private const int WriteAttempts = 3;
    private static readonly VendorDeviceIdentity identity = new("CubePilot", "CubeLAN 8 Port Switch");
    private const VendorDeviceCapabilities Capabilities =
        VendorDeviceCapabilities.Discovery |
        VendorDeviceCapabilities.ReadConfiguration |
        VendorDeviceCapabilities.ApplyConfiguration |
        VendorDeviceCapabilities.ConfirmReadback |
        VendorDeviceCapabilities.Rollback |
        VendorDeviceCapabilities.Export;
    private static readonly JsonSerializerOptions exportOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IDeviceOperationClient deviceClient;
    private readonly ICubeLanConfigurationCodec codec;
    private readonly ILogger<CubeLanDeviceAdapter> logger;

    /// <summary>Initializes the CubeLAN adapter.</summary>
    /// <param name="deviceClient">The correlated MAVLink device-operation client.</param>
    /// <param name="codec">The documented CubeLAN configuration codec.</param>
    /// <param name="logger">The logger.</param>
    public CubeLanDeviceAdapter(
        IDeviceOperationClient deviceClient,
        ICubeLanConfigurationCodec codec,
        ILogger<CubeLanDeviceAdapter> logger)
    {
        this.deviceClient = deviceClient;
        this.codec = codec;
        this.logger = logger;
    }

    /// <inheritdoc />
    public string DeviceType => "cubelan-8-port-switch";

    /// <inheritdoc />
    public async Task<VendorDeviceDiscoveryResult<CubeLanConfiguration>> DiscoverAsync(
        VehicleId vehicleId,
        VendorDeviceAuthentication? authentication = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await ReadAsync(vehicleId, authentication, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Discovered {Product} through vehicle {VehicleId}.", identity.Product, vehicleId);
            return new VendorDeviceDiscoveryResult<CubeLanConfiguration>(
                VendorDeviceStatus.Available,
                "CubeLAN configuration was discovered and read.",
                snapshot);
        }
        catch (TimeoutException)
        {
            return new VendorDeviceDiscoveryResult<CubeLanConfiguration>(
                VendorDeviceStatus.NotDiscovered,
                "No CubeLAN response was received from the documented I²C address 0x50.",
                null);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase))
        {
            return new VendorDeviceDiscoveryResult<CubeLanConfiguration>(
                VendorDeviceStatus.NotConnected,
                "Connect a vehicle before discovering CubeLAN.",
                null);
        }
        catch (CubeLanProtocolException exception)
        {
            logger.LogWarning(exception, "CubeLAN discovery found an unsupported response for {VehicleId}.", vehicleId);
            return new VendorDeviceDiscoveryResult<CubeLanConfiguration>(
                VendorDeviceStatus.Unsupported,
                exception.Message,
                null);
        }
    }

    /// <inheritdoc />
    public async Task<VendorDeviceSnapshot<CubeLanConfiguration>> ReadAsync(
        VehicleId vehicleId,
        VendorDeviceAuthentication? authentication = null,
        CancellationToken cancellationToken = default)
    {
        var result = await deviceClient.ReadAsync(
            vehicleId,
            DeviceOpBustype.I2c,
            Bus,
            Address,
            0,
            ConfigurationLength,
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new CubeLanProtocolException($"Vehicle rejected CubeLAN DEVICE_OP_READ with result {result.ResultCode}.");
        }

        if (result.RegisterStart != 0 || result.Data.Length != ConfigurationLength)
        {
            throw new CubeLanProtocolException(
                $"CubeLAN discovery returned {result.Data.Length} bytes at offset {result.RegisterStart}; expected {ConfigurationLength} bytes at offset 0.");
        }

        var decoded = codec.Decode(result.Data);
        if (!decoded.Success || decoded.Configuration is null)
        {
            throw new CubeLanProtocolException(decoded.Message ?? "The CubeLAN configuration format is unsupported.");
        }

        return new VendorDeviceSnapshot<CubeLanConfiguration>(
            vehicleId,
            identity,
            VendorDeviceTransport.MavLinkDeviceOperation,
            Capabilities,
            decoded.Configuration,
            DateTimeOffset.UtcNow,
            false,
            false);
    }

    /// <inheritdoc />
    public IReadOnlyList<VendorDeviceValidationIssue> Validate(CubeLanConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var issues = new List<VendorDeviceValidationIssue>();
        var portIndexes = configuration.Ports.Select(port => port.PortIndex).ToArray();
        if (portIndexes.Length != 8 || portIndexes.Distinct().Count() != 8 || portIndexes.Any(index => index > 7))
        {
            issues.Add(new VendorDeviceValidationIssue("ports", "CubeLAN requires exactly one configuration for each port 0 through 7."));
        }

        var membershipKeys = configuration.VlanMembership
            .Select(item => (item.SourcePort, item.DestinationPort))
            .ToArray();
        if (membershipKeys.Length != 64 || membershipKeys.Distinct().Count() != 64 ||
            membershipKeys.Any(key => key.SourcePort > 7 || key.DestinationPort > 7))
        {
            issues.Add(new VendorDeviceValidationIssue(
                "vlanMembership",
                "CubeLAN requires one VLAN membership value for every source/destination pair in the 8-by-8 matrix."));
        }

        if (configuration.Registers.Select(item => (item.PhyAddress, item.Register)).Distinct().Count() != configuration.Registers.Count)
        {
            issues.Add(new VendorDeviceValidationIssue("registers", "Preserved CubeLAN register addresses must be unique."));
        }

        return issues;
    }

    /// <inheritdoc />
    public async Task<VendorDeviceApplyResult<CubeLanConfiguration>> ApplyAsync(
        VehicleId vehicleId,
        VendorDeviceSnapshot<CubeLanConfiguration> original,
        CubeLanConfiguration desired,
        VendorDeviceAuthentication? authentication = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(desired);
        if (original.VehicleId != vehicleId)
        {
            return Failure("The read-before-edit snapshot belongs to another vehicle.", null, false, false);
        }

        var issues = Validate(desired);
        if (issues.Count != 0)
        {
            return Failure(string.Join(" ", issues.Select(issue => issue.Message)), null, false, false);
        }

        var originalDocument = codec.Encode(original.Configuration);
        var desiredDocument = codec.Encode(desired);
        try
        {
            await WriteAndConfirmDocumentAsync(vehicleId, desiredDocument, cancellationToken).ConfigureAwait(false);
            var confirmed = await ReadAsync(vehicleId, authentication, cancellationToken).ConfigureAwait(false);
            if (!Equivalent(desired, confirmed.Configuration))
            {
                throw new CubeLanProtocolException("CubeLAN full readback did not match the requested configuration.");
            }

            logger.LogInformation("Applied and confirmed CubeLAN configuration through vehicle {VehicleId}.", vehicleId);
            return new VendorDeviceApplyResult<CubeLanConfiguration>(
                true,
                "CubeLAN configuration applied and confirmed by readback.",
                confirmed,
                false,
                false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "CubeLAN apply failed for {VehicleId}; attempting rollback.", vehicleId);
            var rollbackSucceeded = await TryRollbackAsync(vehicleId, originalDocument).ConfigureAwait(false);
            if (exception is OperationCanceledException)
            {
                throw;
            }

            return Failure(
                rollbackSucceeded
                    ? $"CubeLAN apply failed and the original configuration was restored: {exception.Message}"
                    : $"CubeLAN apply failed and rollback could not be confirmed: {exception.Message}",
                rollbackSucceeded ? original : null,
                true,
                rollbackSucceeded);
        }
    }

    /// <inheritdoc />
    public string Export(CubeLanConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var document = new
        {
            schema = configuration.Schema,
            deviceType = DeviceType,
            ports = configuration.Ports,
            vlanMembership = configuration.VlanMembership
        };
        return JsonSerializer.Serialize(document, exportOptions);
    }

    private async Task WriteAndConfirmDocumentAsync(
        VehicleId vehicleId,
        IReadOnlyList<byte> document,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < document.Count; offset++)
        {
            var desired = document[offset];
            var confirmed = false;
            for (var attempt = 0; attempt < WriteAttempts && !confirmed; attempt++)
            {
                var current = await deviceClient.ReadAsync(
                    vehicleId,
                    DeviceOpBustype.I2c,
                    Bus,
                    Address,
                    checked((byte)offset),
                    1,
                    cancellationToken).ConfigureAwait(false);
                EnsureSuccessfulRead(current, offset);
                if (current.Data[0] == desired)
                {
                    confirmed = true;
                    continue;
                }

                var write = await deviceClient.WriteAsync(
                    vehicleId,
                    DeviceOpBustype.I2c,
                    Bus,
                    Address,
                    checked((byte)offset),
                    new[] { desired },
                    cancellationToken).ConfigureAwait(false);
                if (!write.IsSuccess)
                {
                    continue;
                }

                var readback = await deviceClient.ReadAsync(
                    vehicleId,
                    DeviceOpBustype.I2c,
                    Bus,
                    Address,
                    checked((byte)offset),
                    1,
                    cancellationToken).ConfigureAwait(false);
                EnsureSuccessfulRead(readback, offset);
                confirmed = readback.Data[0] == desired;
            }

            if (!confirmed)
            {
                throw new CubeLanProtocolException($"CubeLAN byte {offset} was not confirmed after {WriteAttempts} attempts.");
            }
        }
    }

    private async Task<bool> TryRollbackAsync(VehicleId vehicleId, IReadOnlyList<byte> originalDocument)
    {
        using var rollbackCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            await WriteAndConfirmDocumentAsync(vehicleId, originalDocument, rollbackCancellation.Token).ConfigureAwait(false);
            var confirmed = await ReadAsync(vehicleId, null, rollbackCancellation.Token).ConfigureAwait(false);
            var decodedOriginal = codec.Decode(originalDocument.ToArray());
            return decodedOriginal.Configuration is not null && Equivalent(decodedOriginal.Configuration, confirmed.Configuration);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "CubeLAN rollback could not be confirmed for {VehicleId}.", vehicleId);
            return false;
        }
    }

    private static void EnsureSuccessfulRead(DeviceOperationResult result, int offset)
    {
        if (!result.IsSuccess || result.RegisterStart != offset || result.Data.Length != 1)
        {
            throw new CubeLanProtocolException(
                $"CubeLAN byte read failed at offset {offset} with result {result.ResultCode}.");
        }
    }

    private static bool Equivalent(CubeLanConfiguration expected, CubeLanConfiguration actual) =>
        expected.Ports.SequenceEqual(actual.Ports) && expected.VlanMembership.SequenceEqual(actual.VlanMembership);

    private static VendorDeviceApplyResult<CubeLanConfiguration> Failure(
        string message,
        VendorDeviceSnapshot<CubeLanConfiguration>? snapshot,
        bool rollbackAttempted,
        bool rollbackSucceeded) =>
        new(false, message, snapshot, rollbackAttempted, rollbackSucceeded);

    private sealed class CubeLanProtocolException(string message) : Exception(message);
}

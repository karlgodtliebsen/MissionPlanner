using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents normalized telemetry-radio link statistics.</summary>
/// <param name="LocalRssi">The local RSSI in device-specific units.</param>
/// <param name="RemoteRssi">The remote RSSI in device-specific units.</param>
/// <param name="TransmitBufferPercent">The remaining transmit-buffer percentage.</param>
/// <param name="LocalNoise">The local noise level in device-specific units.</param>
/// <param name="RemoteNoise">The remote noise level in device-specific units.</param>
/// <param name="ReceiveErrors">The receive error count.</param>
/// <param name="CorrectedPackets">The corrected packet count.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleRadioLinkObservation(int? LocalRssi, int? RemoteRssi, int? TransmitBufferPercent, int? LocalNoise, int? RemoteNoise, ushort ReceiveErrors, ushort CorrectedPackets, DateTimeOffset ObservedAt) : IVehicleObservation;

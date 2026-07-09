# MAVLink message records and decoders

Copy the files into the matching `src/Core/MissionPlanner.MavLink/...` folders.

Add these decoders to your `MavLinkConfigurator` list:

```csharp
new GpsRawIntMessageDecoder(),
new RawImuMessageDecoder(),
new ScaledPressureMessageDecoder(),
new LocalPositionNedMessageDecoder(),
new ServoOutputRawMessageDecoder(),
new MissionCurrentMessageDecoder(),
new NavControllerOutputMessageDecoder(),
new RcChannelsMessageDecoder(),
new VfrHudMessageDecoder(),
new TimeSyncMessageDecoder(),
new PowerStatusMessageDecoder(),
new BatteryStatusMessageDecoder(),
new MemInfoMessageDecoder(),
new Ahrs2MessageDecoder(),
new EkfStatusReportMessageDecoder(),
```

`CommonMavLinkCrcExtraProvider.cs` is included as a replacement with CRC extras for all of the IDs in your current `MessageIds` list.

The decoders use MAVLink wire order, not declaration order. For example, `VFR_HUD` is encoded as `airspeed`, `groundspeed`, `alt`, `climb`, `heading`, `throttle`.

The records deliberately keep some protocol values in raw units and convert the most commonly useful GCS values:

- GPS latitude/longitude and AHRS2 latitude/longitude are converted from `degE7` to degrees.
- GPS altitude and ellipsoid altitude are converted from millimetres to metres.
- GPS yaw is converted from centidegrees to degrees when present.
- MAVLink 2 extension fields are nullable when they may be truncated from the payload.

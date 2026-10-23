# Codex Task 10 — Antenna Tracker and FFT Setup

## Goal

Complete two remaining specialist Optional Hardware areas:

```text
Antenna Tracker
FFT Setup
```

They have different domain concerns and should remain separate views/services.

---

# Part A — Antenna Tracker

## Classic references

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigAntennaTracker.cs
src-v.1.38/Antenna/TrackerUI*
```

Current NextGen already recognizes an AntennaTracker firmware family in firmware/domain mappings.

Distinguish two concepts:

1. **Tracker vehicle configuration** — parameters on an ArduPilot AntennaTracker vehicle.
2. **Tracker operation/control utility** — pointing/tracking behavior driven by a target vehicle/GCS.

Do not combine them ambiguously.

### Configuration

When active firmware family is AntennaTracker, expose metadata-backed settings such as those currently present for:

```text
yaw/pitch servo configuration
PID parameters
yaw range / pitch min/max
slew
orientation
altitude source
RC output calibration
```

Use current parameter names/presence/metadata, not only classic names.

Writes must be explicit and readback-confirmed.

### Test yaw/pitch

If retaining classic actuator test controls:

- require correct tracker firmware/capability;
- bound output;
- clear stop/neutral behavior;
- operation gate;
- do not reuse Copter Motor Test semantics.

### Tracker operation

If a live tracker-control feature is implemented:

- define target source explicitly;
- show current target/location;
- stop tracking on target disconnect;
- do not create a hidden background tracker merely by opening the setup page.

If the full operational tracker is too large, make this task deliver tracker configuration first and expose the operational portion as a clearly documented follow-up, not a fake button.

---

# Part B — FFT Setup

## Classic reference

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigFFT.cs
src-v.1.38/Controls/fftui.cs
```

Classic setup exposes parameters such as:

```text
INS_LOG_BAT_CNT
INS_LOG_BAT_MASK
LOG_BITMASK
```

and launches FFT analysis.

Current NextGen also has DataFlash Logs UI. Inspect:

```text
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/FlightData/Tabs/DataFlashLogsTabView*
```

### FFT setup view

Use metadata-backed parameters.

Explain:

- sample count / frequency relationship;
- selected IMUs;
- required logging bits;
- conflicts such as raw/fast IMU logging where current ArduPilot behavior requires it.

Do not copy stale bit values from classic Mission Planner; obtain enum/bitmask labels from current parameter metadata.

### Analysis

Do not build a second log download path if DataFlash Logs already owns log acquisition.

Prefer:

```text
FFT Setup -> Open DataFlash/FFT Analysis
```

and reuse downloaded log artifacts.

If NextGen lacks FFT analysis, create a focused analysis service:

```text
IFftAnalysisService
FftSpectrum
FftPeak
```

that consumes existing log/sample data.

Keep signal-processing code outside the ViewModel.

Tests should use deterministic synthetic signals (e.g. known sine frequencies).

---

## Tests

Antenna Tracker:

1. tab capability follows tracker firmware/parameters.
2. metadata-backed settings only.
3. write/readback.
4. test output safety/stop.
5. disconnect cancels active operation.

FFT:

6. tab hides when FFT/log parameters absent.
7. bitmask options come from metadata.
8. settings write/readback.
9. synthetic frequency produces expected FFT peak within tolerance.
10. analysis reuses existing log artifacts and does not duplicate download ownership.

---

## Acceptance criteria

Complete when tracker configuration is safe/capability-driven and FFT setup integrates with the existing NextGen logging architecture.

# FFT and vibration analysis

Status: Tasks 1–6 implemented for the first domain/API review (2026-09-29).
Tasks 7–15 remain deferred at the review point specified in
[the task set](tasks/fft/fft-tasks.md). The new domain has no Avalonia, MAVLink,
DataFlash, vehicle-parameter, filesystem or third-party DSP dependency.

## Audit provenance

For future legacy reference, use the
[ArduPilot/MissionPlanner repository](https://github.com/ArduPilot/MissionPlanner).
The local legacy directory was intentionally deleted; see [LEGACY_SOURCE.md](LEGACY_SOURCE.md).
The following audit preserves the exact historical snapshot inspected during this task,
and does not claim that current upstream code is identical.

`src-v.1.38` was removed in commit `e85f3eda1`. The audit inspected its files
read-only with `git show e85f3eda1^:src-v.1.38/...`; it did not restore or change
legacy source. References below identify paths within that historical tree.

| Area | Legacy implementation | Next Gen before this change |
|---|---|---|
| FFT | `ExtLibs/Utilities/fft.cs`: `FFT2`, radix-two transform, periodic Hann, frequency table, linear/dB output; `fft3.cs` also contains a sliding DFT using ALGLIB complex values, and `ExtLibs/Exocortex.DSP/Fourier.cs` contains another Fourier implementation | `Core/Setup/OptionalHardware/FftAnalysis.cs`: direct O(N²) DFT, symmetric Hann, `2/N` scaling, one strongest non-DC bin; not a reusable analysis subsystem |
| Sources | `Controls/fftui.cs`: WAV input; DataFlash `ACC1/2/3`, `GYR1/2/3`, `IMU/IMU2/IMU3`, and `ISBH/ISBD` via `DFLogBuffer` | FFT setup parses manually supplied comma/semicolon/newline-separated numbers and a sample rate; no DataFlash decoding |
| Batch IMU | `ISBH` carries `N`, `type`, `instance`, `smp_rate`, `mul`; `ISBD` supplies packed axis arrays associated with the batch number | No IMU batch-sample provider found |
| Spectrum UI | `Controls/fftui.cs`: WinForms/ZedGraph, axis curves, averages, frequency/RPM tooltip | `FftSetupViewModel`: synchronous service call and a textual peak result; advanced FFT/spectrogram catalog entries are not implementations |
| Spectrogram | `ExtLibs/Utilities/Spectrogram.cs` and `Controls/SpectrogramUI.cs`: windowed FFT, batch and regular log data, ImageSharp heatmaps with ZedGraph presentation | No STFT or spectrogram control found |
| Notch | `GCSViews/ConfigurationView/ConfigArducopter*`: `INS_HNTCH_ENABLE`, `FREQ`, `BW`, `ATT`, `MODE`, `REF`, `HMNCS`, `OPTS` controls | `ExtendedTuningProfileCatalog`: primary `INS_HNTCH` and secondary `INS_HNTC2` descriptors for `FREQ/BW/ATT/MODE/HMNCS`, plus gyro low-pass descriptors; no spectrum/filter assessment |
| Logging setup | `GCSViews/ConfigurationView/ConfigFFT.cs`: metadata-backed `INS_LOG_BAT_CNT`, `INS_LOG_BAT_MASK`, `LOG_BITMASK`, warning about IMU_RAW/IMU_FAST, FFT launch | Optional-hardware tab discovery uses FFT/INS_LOG_BAT parameter presence; DataFlash Logs view model explicitly remains a placeholder |

Regular legacy sources use `AccX/Y/Z` or `GyrX/Y/Z` and `TimeUS`/`SampleUS` to
estimate sampling rate. Batch data uses the header sample rate and multiplier.
These are historical implementation facts, not guarantees about current firmware.
Batch sequencing, gaps, units and scaling must be verified against representative
logs when implementing Task 7. Generated MAVLink `ISBD_LINK_STATUS` is unrelated
to DataFlash `ISBD` and must not be used as its decoder.

Reusable concepts are explicit sensor/axis selection, timestamp-derived sample
rates, batch identity/scaling, overlapping windows, spectrum averages and source
provenance. Do not copy WinForms, global connection access, direct parameter writes,
integer-truncated frequency calculations or historical log bit values. In particular,
sample count controls bin spacing; Nyquist is determined by sample **rate**.

The current UI has a custom Avalonia `RadioChannelHistoryPlot`; its drawing approach
is a potential reference, but it is not a general spectral chart or heatmap library.
No general-purpose charting package is referenced by `MissionPlanner.App.csproj`.

## Implemented domain and numerical contract

`src/Core/MissionPlanner.Analysis` is a standalone .NET 10 project in the solution.
The task set explicitly requested this independent boundary. It uses
`System.Numerics.Complex` with a small radix-two FFT, avoiding a new package and
avoiding coupling the generic domain to the older Core setup service.

The existing `IFftAnalysisService` and its consumers are unchanged pending review.
Its old arbitrary-length input and amplitude semantics differ from the new API;
replacing it silently would be a breaking behavioral change. After review, migrate
it through an explicit adapter and retire its DFT rather than adding a third path.

```text
uniform samples + sample rate + options
                ↓
           FftAnalyzer
                ↓
       FrequencySpectrum → PeakDetector → HarmonicDetector
                ↓                              ↓
     full numerical evidence          candidate relationships
                └──────── VibrationAnalyzer ────┘

long uniform sequence → SpectrogramAnalyzer → timed spectrum frames
```

- `Analyze(ReadOnlySpan<double>, double, FftOptions)` requires exactly `Size`
  finite samples and a finite positive sample rate. Size must be a power of two,
  at least four. No implicit padding, truncation, interpolation or resampling.
  Non-power-of-two transforms are explicitly unsupported at this checkpoint.
- Hann is periodic (`0.5 - 0.5*cos(2πn/N)`); rectangular is also available.
  Optional arithmetic-mean removal happens before windowing. The removed mean
  and options are retained with the result.
- Output is one-sided **peak amplitude**, in the input unit, corrected by window
  coherent gain. Interior bins are doubled; DC and Nyquist are not. It is not
  RMS, power spectral density, or decibels. An isolated bin-centered sinusoid has
  its input amplitude; off-bin tones retain expected window leakage/scalloping.
- Bins include DC and Nyquist. Spacing is `sampleRate / Size`. Source aliasing
  cannot be detected from sampled data. A Hann main lobe is broader than one bin;
  reported spacing must not be presented as guaranteed two-tone resolution.
- `PeakDetector` excludes DC and retains Nyquist when it is a local maximum.
  Threshold is the maximum of absolute amplitude, a fraction of the strongest
  non-DC bin, and median non-DC amplitude times a configurable noise multiplier.
  Peaks must strictly exceed the threshold. A flat local plateau selects its
  first bin. Each peak retains bin index, amplitude, noise estimate and threshold.
  The median is a heuristic floor, not an independently measured noise PSD.
- `HarmonicDetector` requires an observed fundamental and at least three measured
  orders. It does not invent missing fundamentals. Matching tolerance is
  `(order + 1) * spacing / 2`, capped at 10% of the fundamental to avoid broad
  low-frequency matches. Each member retains its original peak, order, expected
  frequency, signed residual and tolerance. A bin cannot satisfy multiple orders
  within a candidate. Alternative candidates remain visible.
- Harmonic confidence is the average normalized residual fit of higher orders
  multiplied by order coverage (`matched members / highest matched order`). It is
  a reproducible heuristic score in [0,1], not a probability or a defect diagnosis.
- `SpectrogramAnalyzer` supports configurable FFT size/window, integer overlap,
  and an inclusive frequency range constrained to [0, Nyquist]. Only complete
  windows are analyzed. The result reports unused trailing samples. Each frame
  retains its first sample index, sampled-interval center time and selected bins.
  A range containing no bin centers is rejected. Inputs, including unused tails,
  must be finite. Cancellation is checked between windows. Computation is
  synchronous and scheduling-neutral; a future UI must invoke it on a worker.
- `VibrationAnalyzer` accepts source name, axis and unit, retaining the full
  spectrum, peaks and candidate relationships separately from assessments.
  Assessments say that harmonic evidence is consistent with periodic excitation;
  motor/propeller attribution requires RPM evidence. Empty results are not a
  declaration of mechanical health.

Results use immutable collections. Services are stateless and safe for independent
concurrent calls. Composed services receive their analyzers by constructor injection.
No application registrations are added until the domain review and integration phase.
The batch API materializes every frame; long-log streaming, memory budgets and
incremental presentation belong in the later log/application integration design.

## Verification

Verified 2026-09-29: all 37 analysis tests pass with no build warnings in the new
projects. The full solution builds with `--no-restore`: 0 errors, 55 warnings in
existing projects. No CS1591/CS1587 warnings were reported. The full solution test
suite and hardware tests were not run; no UI or hardware behavior was changed.

`MissionPlanner.Analysis.Tests` uses a seeded `SignalBuilder` with sine mixtures,
DC, uniform noise, and a linear chirp. Coverage includes the 73/146/219 Hz and
163/326/489 Hz reference series, unrelated peaks, amplitude normalization, an
independent direct-transform oracle, both windows, DC and Nyquist endpoints,
between-bin and near-Nyquist signals, noisy signals, threshold behavior, invalid
samples/rates/sizes, zero data, STFT timing/range/tail handling and cancellation.

Run:

```powershell
dotnet test C:\Projects\MissionPlanner\src\Tests\MissionPlanner.Analysis.Tests\MissionPlanner.Analysis.Tests.csproj
```

## Review boundary and later work

Tasks 1–6 are the first review deliverable. `ResonanceDetector` and
`MotorFrequencyCorrelator` were suggested in the Task 6 directory sketch, but their
behavior is explicitly defined by Tasks 8–9; they are deferred rather than shipped
as empty classes or unsupported diagnoses.

After review:

1. Task 7 needs a DataFlash parser/provider boundary; there is no complete existing
   parser workflow to simply connect. Supply immutable uniform sample segments,
   units, IMU identity, start/end times and batch/gap diagnostics to this domain.
2. Tasks 8–9 add time-aligned RPM evidence and resonance candidates. Output percentage
   must never be relabeled as RPM. Retain numeric correlation/amplification evidence.
3. Tasks 10–11 add background execution and spectrum/heatmap UI, selection, cursor
   inspection, time ranges and log timeline synchronization.
4. Task 12 compares matched/interpolated physical frequencies across resolutions.
5. Tasks 13–14 consume metadata-backed parameter snapshots, assess attenuation
   regions and label offline simulations distinctly; no automatic FC writes.
6. Task 15 adds small regression datasets and metadata. Current synthetic fixtures
   are generated in tests; no large logs have been committed.

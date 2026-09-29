# FFT and vibration analysis

Status: Tasks 1–6 reviewed and approved by the user; Tasks 7–15 implemented as the
initial offline integration (2026-09-29). See the integration contracts and verification
below and [the task set](tasks/fft/fft-tasks.md). The numerical domain has no Avalonia, MAVLink,
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
Batch sequencing, gaps, units and scaling still need validation against representative
real flight logs. Generated MAVLink `ISBD_LINK_STATUS` is unrelated
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

The existing `IFftAnalysisService` is now a compatibility adapter to `FftAnalyzer`;
its separate O(N²) DFT has been retired. Manual text input uses the largest complete
power-of-two prefix, requires at least four finite samples, and explicitly reports
`SamplesUsed` and `UnusedTailSamples`. Magnitudes now have the domain's coherent-gain
normalization. The setup page reports these changes and performs analysis on a worker.
The domain FFT's exact-length contract is unchanged.

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
Application registrations now live in `DomainConfigurator`. The application layer
limits materialized data and frames as described below; the numerical API remains
independent of those application budgets.

## Verification

The initial Tasks 1–6 checkpoint passed 37 numerical tests and a full solution build.
After the approved integration, validation covers the expanded numerical suite,
DataFlash/Core suite and Avalonia suite. Final command results are recorded below.
No hardware operations were exercised.

Final integration verification (2026-09-29):

| Command / scope | Result |
|---|---|
| `dotnet build src/MissionPlanner.slnx --no-restore --verbosity quiet` | Passed; 0 errors, 16 warnings in existing Core test files on the final incremental build |
| `dotnet test src/Tests/MissionPlanner.Analysis.Tests/MissionPlanner.Analysis.Tests.csproj --no-build --no-restore` | 46 passed |
| `dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~Fft` | 15 passed, including the final time-bracketing regression |
| Core suite excluding `Category=ManualHardware` and `NetworkSitlTests` | 989 passed, five existing serial tests skipped; run before adding the final time-bracketing test |
| Avalonia suite filtered to `FullyQualifiedName~FftAnalysisViewTests` | 3 passed: compiled themed view/plot modes, load/analyze/baseline/export, deactivation/reactivation |
| Full Avalonia suite | 337 passed, 10 failed in existing navigation-contract/document/layout tests outside FFT |
| Existing Avalonia tests with the new FFT test class excluded | 333 passed, 11 failed; the same failure areas persisted, with an additional servo-rendering failure |

The full UI suite is **not green**. Failures include stale `Views/ConfigTuning/Tabs`
paths, firmware menu expectations, expected four versus actual five diagnostic menu
items, and headless document/dispatcher/layout failures (FirmwareDocument,
SetupInformation, CompassInformation, DiagnosticPanel, Readiness; ServoOutput in
the isolation run). They do not depend on execution of the new FFT test class.
No unrelated UI files or assertions were changed to mask those failures.
No new FFT-file warnings or CS1591/CS1587 warnings appeared in the checked build logs.

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

## DataFlash integration (Task 7)

`Core/Analysis/DataFlashRecordReader` consumes self-describing DataFlash binary
packets or FMT-based text exports from a caller-owned stream. It uses packet lengths
from FMT and decodes only relevant IMU/motor records. Unsupported field layouts,
missing FMT, corrupt packet boundaries and truncated packets fail explicitly. It is
an offline analysis importer, not a general log browser or controller downloader.

`DataFlashImuSampleProvider` supports modern `GYR`, `ACC`, `IMU` plus legacy numbered
variants, using `I` where present and `SampleUS` before `TimeUS`. Axis values remain
in logged rad/s or m/s². Regular sequences split on non-increasing timestamps, gaps
or interval jitter exceeding max(2 µs, 2% of the established interval). Segments
with cumulative timing deviation above 0.25 sample are rejected. No interpolation
is applied to IMU samples. Sample rate is derived from accepted segment timestamps.

ISBH/ISBD decoding uses header batch number, type, instance, sample count, first
sample time, sample rate and multiplier. It requires sequential 32-value axis chunks;
scaled samples are raw int16 divided by `mul`. Incomplete or out-of-order batches
are discarded with diagnostics. Batches remain separate selectable segments, so
sampling pauses cannot be hidden by concatenation. Spectrogram/correlation currently
operate within one uniform segment, not across disjoint batch intervals.

The inspected ArduPilot definitions are `libraries/AP_InertialSensor/LogStructure.h`
and `libraries/AP_ESC_Telem/LogStructure.h` at local checkout revision
`e9fc86200fac8d5d77d68a90a212eaea9c47dedf`. Fixtures are generated from those layouts;
they do not substitute for real firmware/log compatibility testing.

Import budgets: seekable artifacts up to 256 MiB, ten million records, six million
retained/pending axis values, one million values per regular axis segment, one
million motor observations and 20,000 output segments. Analysis is limited to 2,048
frames and two million time-frequency bins. Exceeding a budget requests a shorter
export/interval rather than silently dropping data. Parsing and DSP run on workers.

## RPM and resonance evidence (Tasks 8–9)

ESC `RPM` and RPM sensor `rpm1/rpm2` values retain their logged index and identity.
RPM sensor-to-motor mapping is explicitly unknown. `RCOU` outputs retain PWM units
and are never converted to RPM. Within each frame, the dominant measured band is
selected independently of RPM. RPM is linearly interpolated only between surrounding
telemetry points at most 0.5 seconds apart; there is no extrapolation over gaps.

`MotorFrequencyCorrelator` retains every time/RPM/frequency/amplitude pair, Pearson
correlation, mean expected/observed frequencies, mean difference and RMS residual.
Constant speed or frequency has undefined correlation. The UI currently uses shaft
order 1; the numerical API supports an explicit positive order. A high correlation
alone does not establish that the frequency equals shaft frequency: inspect the
residual and source identity as well.

`ResonanceDetector` requires at least five measurements, positive correlation >=0.7,
frequency coverage on both sides of a candidate band and amplitude amplification
>=3 relative to the out-of-band median. The strongest peak defines the candidate;
the application uses width max(10 Hz, four bins). Confidence is correlation times
`1 - 1/amplification`, a heuristic rather than a probability. Changing excitation,
load or a transient remains an alternative explanation. No specific defect is asserted.

## User workflow and comparison (Tasks 10–12)

The shared `FftAnalysisView` is available under **Logs → FFT / Vibration**,
**Flight Data → DataFlash Logs**, and **Optional Hardware → FFT → FFT / Vibration
Analysis**. The Logs path is available without vehicle parameter discovery.

1. Open an existing `.bin` or FMT-based `.log` artifact.
2. Select an IMU/signal/axis/uniform segment. Inspect source diagnostics.
3. Set start/end in log boot-time seconds, FFT size and frequency range (maximum 0
   means Nyquist), then Analyze. The spectrum is the arithmetic mean of linear
   amplitudes over complete 50%-overlapped Hann windows, with unused tail reported.
4. Switch Spectrum/Spectrogram and hover for original numerical bins. Heatmap
   rendering max-pools to at most 256×128 display cells; exported frames are not
   decimated. Its −60…0 dB colors are relative to the largest selected-frame amplitude.
5. Capture a completed spectrum as baseline, then open/select the current dataset
   and Analyze. Comparisons use matching units, window and DC settings and linear
   interpolation onto the coarser physical-frequency grid within shared Nyquist.
   Results show frequency, baseline, current and change. Different resolution/window
   leakage can still affect apparent amplitudes; this is not a PSD comparison.
6. Export evidence as schema-versioned JSON. It includes all source values, bins,
   frames, threshold evidence, harmonic matches, paired RPM observations, baseline,
   captured analysis choices, parameter snapshot and proposed static filters.

The shared source clock synchronizes interval selection and spectrogram inspection.
There is no separate DataFlash replay timeline to synchronize with. Loading/analysis
can be cancelled; deactivation cancels the view lifetime and suppresses late results.
Plots represent the last completed operation; press Analyze after changing controls.

## Notch analysis and simulation (Tasks 13–14)

`NotchParameterAnalysisService` reads actual connected registry values or saved
name/value `.param/.params/.csv/.txt` files. Saved files require an explicit firmware
family for metadata lookup. Descriptions come from `IVehicleParameterMetadataService`;
missing metadata is marked unavailable. Snapshots are frozen and identified by source,
not live subscriptions. The service has no parameter-write dependency or operation.

Static `INS_HNTCH` and `INS_HNTC2` bands require an enabled, complete configuration,
MODE=0, OPTS=0, valid FREQ/BW/ATT and an integer harmonic mask. Unknown/dynamic
settings are listed as limitations, never interpreted as disabled. Harmonic center
and bandwidth scale with order. Peaks are labeled inside/outside supported nominal
center ± bandwidth/2 regions; actual attenuation is not claimed. Harmonics at/above
the selected sample Nyquist are excluded from simulation with a note.

`NotchFilterSimulator` uses a generic digital biquad notch with Q=center/bandwidth.
A dry/wet blend sets center attenuation; transfer magnitudes multiply for cascades.
It applies the response to the measured spectrum and keeps measured and simulated
amplitudes separate. Current supported static filters (blue) and a user-proposed
static notch (orange) can be compared to measured data (green). It is not ArduPilot's
exact filter, does not reproduce dynamic tracking, phase/transients, or undo filters
already applied to the logged signal, and does not recommend or write FC parameters.

## Regression infrastructure and remaining validation (Task 15)

`TestData/Frequency/Synthetic` contains discoverable JSON signal manifests.
`TestData/Frequency/DataFlash` contains a small generated text fixture plus expected
frequency/amplitude metadata; binary batch fixtures are constructed in tests.
See [the fixture guide](../TestData/Frequency/README.md). No large logs are committed.

The supplied [FFT synthetic fixture pack](../TestData/MissionPlanner-FFT-Synthetic-TestData/README.md)
remains in `TestData/MissionPlanner-FFT-Synthetic-TestData`. The analysis test project
links and copies its CSV, metadata, README and checksum files into its output directory;
the source data does not need moving into the test project. `SuppliedFftDatasetTests`
adds 18 regression cases covering all 16 datasets, using the actual CSV samples rather
than regenerating the signals. It also checks sample counts, finite values, uniform
timestamps and content checksums. Checksum verification restores the pack's original
line endings (CRLF for CSV, LF for JSON/Markdown), allowing Git checkout conversion.

Stationary comparisons use 4096-sample Hann windows with 50% overlap and mean removal;
time-localized checks use 512-sample windows with 50% overlap. Frequency expectations
come from the supplied metadata or the STFT bin spacing. Checks cover peaks, harmonic
families and negative controls, DC removal, near-Nyquist frequencies, sweep ridges,
intermittent energy, measured CSV RPM correlation, localized amplification,
motor-3 harmonic increases against the baseline, and relative axis coupling.
The near-harmonic fixture checks observed peaks without imposing a binary family
classification. The 137 Hz source in dataset 11 has only two observed orders, so it
must retain its peaks without meeting the detector's three-member series threshold.
Dataset 10 has no measured RPM: its resonance test explicitly uses the known
synthetic sweep as excitation reference. These are evidence tests, not validation
of real-world fault diagnoses or DataFlash ingestion; the CSV loader is test-only.

Validation: `dotnet test src/Tests/MissionPlanner.Analysis.Tests/MissionPlanner.Analysis.Tests.csproj --no-restore`
passes all **64 tests**, including the **18 supplied-fixture cases**.

Automated tests cover numerical models, regular and batch parsing, malformed inputs,
gap handling, metadata-backed static/dynamic behavior, read-only saved import,
unit mismatch, view lifetime cancellation, evidence export and compiled view layout
in both light/dark themes and both plot modes. Real flight logs, connected-controller
parameter capture and interactive desktop/browser cursor behavior still require
manual validation. No controller hardware operation was performed.

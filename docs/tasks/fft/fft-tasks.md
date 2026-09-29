Yes. 
There is enough now to create implementation-ready Codex tasks. 
I would split it into staged tasks so Codex does not mix the DSP/domain work with the Avalonia UI and log integration too early.

## FFT / Vibration Analysis — Codex task set

### Execution status — 2026-09-29

Tasks 1–6 are implemented for the first review. See
[FftAnalysisDesign.md](../../FftAnalysisDesign.md) for the legacy audit, numerical
contracts, implementation choices and limitations. The standalone analysis project
has no Avalonia, MAVLink or DataFlash dependency. Tasks 7–15 remain pending this review.

Validation: `dotnet test` on `MissionPlanner.Analysis.Tests` passed all 37 tests.
`dotnet build src/MissionPlanner.slnx --no-restore` succeeded with 0 errors and 55
warnings in existing projects. No new analysis-project warnings or CS1591/CS1587
warnings were reported. The full solution test suite and hardware tests were not run.

### Task 1 — Audit existing Fourier/FFT functionality

**Goal:** Establish what already exists in MissionPlanner Next Gen and in the original [ArduPilot/MissionPlanner repository](https://github.com/ArduPilot/MissionPlanner) before implementing anything. The local `src-v.1.38` directory has been deleted; see [legacy source guidance](../../LEGACY_SOURCE.md).

**Instructions for Codex:**

* Search the current solution for FFT, Fourier, vibration, IMU batch sampling, harmonic notch, spectrogram, gyro and accelerometer analysis.
* Inspect equivalent functionality in the upstream ArduPilot/MissionPlanner repository and record the inspected commit or tag. Historical audit results may retain the exact Git revision of the former local snapshot.
* Identify:

  * existing FFT implementation/library;
  * log message types used;
  * DataFlash messages involved;
  * existing plotting functionality;
  * IMU batch-sampling support;
  * harmonic-notch support;
  * parameter names used by the old implementation.
* Do not copy old UI architecture into Next Gen.
* Produce `docs/FftAnalysisDesign.md` documenting:

  * old implementation;
  * reusable concepts;
  * missing functionality;
  * proposed Next Gen architecture.
* Update `FEATURES.md` with the FFT/vibration-analysis feature area.

**Acceptance:** Documentation clearly distinguishes legacy functionality, currently implemented Next Gen functionality and proposed additions.

---

### Task 2 — Create reusable frequency-analysis domain

Create an analysis subsystem independent of Avalonia, MAVLink and log-file UI.

Suggested structure:

```text
MissionPlanner.Analysis/
    Frequency/
        FftAnalyzer.cs
        FftOptions.cs
        FrequencySpectrum.cs
        FrequencyBin.cs
        SpectralPeak.cs
        PeakDetector.cs
        HarmonicSeries.cs
        HarmonicDetector.cs
        WindowFunction.cs
        WindowFunctions.cs
```

Core API should accept samples plus sample frequency, not ArduPilot messages:

```csharp
FrequencySpectrum Analyze(
    ReadOnlySpan<double> samples,
    double sampleRateHz,
    FftOptions options);
```

Support initially:

* Hann window;
* configurable FFT size;
* DC removal;
* amplitude normalization;
* frequency resolution calculation;
* Nyquist validation;
* one-sided spectrum;
* peak detection;
* configurable noise/peak thresholds.

Do not introduce vehicle-specific assumptions into this layer.

---

### Task 3 — Build deterministic FFT test-signal generator

This is important enough to be its own task.

Add test infrastructure capable of generating:

```csharp
SignalBuilder
    .WithSampleRate(1000)
    .WithDuration(...)
    .AddSine(73, amplitude: 1.0)
    .AddSine(146, amplitude: 0.4)
    .AddSine(219, amplitude: 0.2)
    .AddNoise(...);
```

Tests must cover:

* single sinusoid;
* multiple frequencies;
* harmonics;
* DC offset;
* random noise;
* signal below noise floor;
* frequency between FFT bins;
* frequencies near Nyquist;
* insufficient sample data;
* non-power-of-two sample counts if supported.

Primary reference test:

```text
73 Hz
146 Hz
219 Hz
```

The analyzer must correctly identify the fundamental and harmonics within the mathematically expected frequency resolution.

---

### Task 4 — Harmonic-series detection

Implement detection of relationships rather than treating every FFT peak independently.

For:

```text
163 Hz
326 Hz
489 Hz
```

produce something conceptually equivalent to:

```csharp
HarmonicSeries
{
    FundamentalHz = 163,
    Harmonics =
    [
        { Order = 1, FrequencyHz = 163 },
        { Order = 2, FrequencyHz = 326 },
        { Order = 3, FrequencyHz = 489 }
    ]
}
```

Account for FFT resolution/tolerance.

Do not require exact integer-frequency matches.

Return confidence/evidence rather than an unqualified diagnosis.

Tests must include misleading/non-harmonic peaks.

---

### Task 5 — Add spectrogram/time-frequency analysis

A single FFT hides changes over time.

Implement STFT-based analysis:

```text
samples
   ↓
overlapping windows
   ↓
FFT per window
   ↓
TimeFrequencySpectrum
```

Suggested models:

```text
Spectrogram
SpectrogramFrame
TimeFrequencyBin
```

Configuration:

```text
Window size
Overlap
Window function
Frequency range
```

Test with a frequency sweep where the expected dominant frequency rises over time.

This provides the basis for later RPM/throttle correlation and resonance detection.

---

### Task 6 — Implement vibration-analysis domain

Build on the generic frequency subsystem.

Suggested structure:

```text
MissionPlanner.Analysis/
    Vibration/
        VibrationAnalyzer.cs
        VibrationAnalysis.cs
        VibrationAxis.cs
        VibrationSource.cs
        VibrationAssessment.cs
        ResonanceCandidate.cs
        ResonanceDetector.cs
        MotorFrequencyCorrelator.cs
```

The analysis should distinguish observations from interpretations.

For example:

```text
Observation:
Strong spectral peak at 172.4 Hz.

Observation:
Second harmonic detected at 344.1 Hz.

Observation:
Peak frequency increases with motor RPM.

Assessment:
Evidence is consistent with a motor/propeller-related vibration source.
```

Do **not** report a specific mechanical defect merely because a spectral peak exists.

---

### Task 7 — ArduPilot/DataFlash IMU integration

Connect the generic analysis subsystem to MissionPlanner's existing log infrastructure.

Support relevant gyro/accelerometer data and, where available, IMU batch-sampling data.

Architecture:

```text
DataFlash
    ↓
ArduPilot log parser
    ↓
ImuSampleSeries
    ↓
MissionPlanner.Analysis
```

The analysis project must remain unaware of DataFlash.

Create an adapter such as:

```csharp
DataFlashImuSampleProvider
```

Expose:

```text
IMU instance
Axis
Sample rate
Start/end time
Sample count
Batch information
```

Handle irregular or incomplete sample sequences explicitly.

---

### Task 8 — Correlate motor output/RPM with vibration

Where logs provide suitable information, correlate:

```text
Motor RPM / ESC telemetry / motor output
                   +
             FFT/Spectrogram
                   ↓
         Frequency correlation
```

This should support detection of frequency bands that track motor speed.

Model evidence explicitly:

```csharp
MotorFrequencyCorrelation
{
    MotorIndex
    Correlation
    ExpectedFrequencyHz
    ObservedFrequencyHz
    DifferenceHz
}
```

Do not assume that motor-output percentage equals RPM.

Use actual RPM/ESC telemetry when available; otherwise identify the correlation source and its limitations.

---

### Task 9 — Resonance detection

Use spectrogram/time-varying data to distinguish a frequency-following vibration from a structural resonance candidate.

Example:

```text
Motor frequency moves:

80 → 130 → 180 → 230 Hz

Amplitude becomes disproportionately large near:

175–185 Hz
```

Return:

```text
ResonanceCandidate
    FrequencyRange
    PeakFrequency
    RelativeAmplification
    Evidence
    Confidence
```

The output should say **resonance candidate**, not assert that the frame is defective.

---

### Task 10 — Implement Avalonia FFT Analysis view

Only after the analysis domain is stable.

Add an FFT/Vibration Analysis view consistent with MissionPlanner Next Gen styling and existing controls.

Suggested layout:

```text
┌──────────────────────────────────────────────────────────┐
│ FFT / Vibration Analysis                                │
├──────────────────────────────────────────────────────────┤
│ Source   [IMU1 ▼]  Signal [Gyro ▼] Axis [X ▼]           │
│ Time     [.........................]                     │
├──────────────────────────────────────────────────────────┤
│                                                          │
│                   Frequency Spectrum                     │
│                                                          │
├──────────────────────────────────────────────────────────┤
│ Peaks                                                    │
│ 163 Hz       Fundamental                                 │
│ 326 Hz       Harmonic 2                                  │
│ 489 Hz       Harmonic 3                                  │
├──────────────────────────────────────────────────────────┤
│ Analysis                                                 │
│ Strong harmonic vibration series detected...            │
└──────────────────────────────────────────────────────────┘
```

Use existing MissionPlanner charting infrastructure where suitable.

Keep analysis out of the ViewModel.

---

### Task 11 — Add spectrogram UI

Provide switchable:

```text
Spectrum | Spectrogram
```

views.

The spectrogram should have:

* time on X;
* frequency on Y;
* magnitude represented visually;
* cursor inspection;
* selectable frequency range;
* selectable time interval;
* synchronization with the source log timeline where practical.

Do not compute STFT on the UI thread.

---

### Task 12 — Baseline comparison

Add comparison of two datasets:

```text
Baseline
vs
Current
```

Examples:

* before/after replacing propeller;
* hard vs soft FC mounting;
* before/after filter changes;
* old vs new frame;
* healthy vs suspected mechanical problem.

Produce structured differences:

```text
Frequency    Baseline    Current    Change
------------------------------------------------
147 Hz       ...         ...        ...
294 Hz       ...         ...        ...
```

Do not compare FFT bins naïvely when resolutions differ; normalize/interpolate appropriately.

---

### Task 13 — Harmonic-notch analysis integration

Integrate existing ArduPilot parameter metadata and connected/saved parameter snapshots.

Read applicable filter parameters from the parameter model rather than hard-coding metadata.

Present:

```text
Observed vibration spectrum
        +
Current filter configuration
        ↓
Filter Analysis
```

Initially this should be **analysis-only**.

Show:

* dominant frequencies;
* detected harmonics;
* current notch configuration;
* whether significant peaks appear inside/outside configured attenuation regions;
* metadata-backed explanation of relevant parameters.

Do not automatically change FC parameters.

---

### Task 14 — Filter simulation

Add offline simulation:

```text
Raw spectrum/signal
       +
Proposed filter configuration
       ↓
Predicted filtered result
```

Allow current vs proposed configuration comparison.

The UI should clearly distinguish:

```text
Measured
```

from:

```text
Simulated
```

results.

This is an important prerequisite before we ever implement automatic filter recommendations.

---

### Task 15 — FFT regression dataset infrastructure

Create:

```text
TestData/
    Frequency/
        Synthetic/
        DataFlash/
```

Do not initially commit enormous logs.

Support test metadata such as:

```text
dataset: damaged-prop-example
expected:
    fundamental: ...
    harmonics: [...]
    notes: ...
```

Make regression tests able to execute against small representative datasets.

---

## Review point

I would deliberately stop Codex after **Tasks 1–6** for our first review.

At that point we should have:

```text
Synthetic signals
      ↓
FFT
      ↓
Spectrum
      ↓
Peak detection
      ↓
Harmonic detection
      ↓
Spectrogram
      ↓
Vibration assessment
```

with **no Avalonia and no DataFlash dependency**.

That gives us an opportunity to inspect the domain/API before coupling it to MissionPlanner's logs and UI.

After that review, Tasks 7–15 can connect it to actual ArduPilot data.

One additional design constraint I'd give Codex across the entire task set: **retain the raw numerical evidence behind every diagnostic conclusion**. Later, when we add the AI diagnostic layer, we want to pass structured facts such as *“172.4-Hz peak, magnitude X, harmonic at 344.1 Hz, RPM correlation 0.91”* rather than asking AI to interpret screenshots of FFT plots. That will also make the non-AI diagnostics independently useful.

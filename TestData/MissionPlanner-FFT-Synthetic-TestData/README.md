# MissionPlanner Next Gen — FFT/Vibration Synthetic Test Data

This fixture pack provides deterministic signals for FFT, peak detection, harmonic detection,
STFT/spectrogram, motor-frequency correlation, resonance detection, and vibration-assessment tests.

## Format

Every dataset directory contains:

- `samples.csv` — time-domain samples. `time_s` is always the first column.
- `expected.json` — generation parameters and expected/intentional features.

`manifest.json` indexes the full suite.

Default sample rate: **1000 Hz**. Random components use a fixed seed, so the suite is reproducible.

## Important interpretation rule

Expected metadata distinguishes known injected evidence from diagnosis. In particular, the
near-harmonic dataset intentionally does not prescribe a binary classification: it is intended
to exercise the implementation's configured tolerance/confidence policy.

## Suggested test progression

1. 01–06: FFT numerical correctness, windowing, DC removal and Nyquist handling.
2. 07–08: false-positive/tolerance tests for harmonic detection.
3. 09–12: STFT/spectrogram and time-localized behaviour.
4. 13: RPM/frequency correlation.
5. 14–16: higher-level vibration assessment and multi-axis/multi-motor analysis.

## Dataset summary

01. SingleTone73 — pure 73 Hz.
02. Harmonics73 — 73/146/219 Hz harmonic family.
03. NoisyHarmonics73 — same family plus broadband Gaussian noise.
04. DcOffset73 — validates DC removal.
05. BetweenBins73_35 — leakage/window/interpolation case.
06. NearNyquist470 — 470 Hz with a 500 Hz Nyquist frequency.
07. NegativeNonHarmonic — 71/153/247 Hz; must not become one harmonic family.
08. NearHarmonicImperfect — 73/145/221 Hz confidence/tolerance boundary.
09. FrequencySweep50-300 — linear chirp for spectrogram tests.
10. Resonance180 — chirp amplified near 180 Hz.
11. TwoHarmonicSources — independent 83 Hz and 137 Hz families.
12. IntermittentFault160 — 160/320 Hz event only from 5–12 s.
13. ChangingRpm80-240 — chirp plus explicit RPM/reference channel.
14. FourMotorHealthy — four close motor fundamentals with weak harmonics.
15. FourMotorMotor3Fault — motor 3 has deliberately elevated H2/H3.
16. ThreeAxisMechanical — same source couples differently into X/Y/Z.

These are synthetic engineering fixtures, not evidence that a particular real-world mechanical
fault necessarily produces the same spectrum.

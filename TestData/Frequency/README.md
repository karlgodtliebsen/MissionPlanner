# Frequency regression fixtures

`Synthetic/*.json` defines small deterministic signal mixtures and expected peak
relationships. The analysis tests discover and execute these manifests automatically.

`DataFlash/regular-128.dftext` is a small FMT-based text fixture generated from a
128 Hz sine, 1024 Hz sample rate, 256 samples, rounded microsecond timestamps.
Its adjacent JSON records provenance and expectations. Core regression tests load
the manifest, decode the file and check the measured spectrum. The non-`.log`
extension keeps the fixture out of the repository's generated-log ignore rules.
Binary ISBH/ISBD fixtures are constructed deterministically in the Core tests.

No fixture is evidence of a damaged propeller or other real defect. Add small,
permission-cleared real log excerpts with explicit firmware/schema provenance before
claiming flight-log validation. Avoid committing whole flight logs; retain only the
necessary FMT definitions, source records and time-aligned RPM evidence.

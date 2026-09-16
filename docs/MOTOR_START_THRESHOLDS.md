# Guided motor start thresholds

The existing Motor Test page now has a guided assistant. Start requires an explicit
props-removed acknowledgement. The workflow never arms the vehicle. It derives all
motor steps and test ordering from the connected frame and resolves physical
outputs with the existing MotorOutputResolver.

Each motor starts at 5%. Pulse sends a one-second percent Motor Test through the
existing guarded actuator service. Increase advances by 1% without sending a
command. After a successful pulse, the user explicitly confirms reliable rotation;
an acknowledgement alone is not treated as evidence that the motor moved.
The next motor then starts at 5%. Motor number, frame factors, physical output,
test percentage, and observed thresholds remain visible.

The recommendation uses the highest measured threshold plus two percentage points
for MOT_SPIN_ARM, then another three for MOT_SPIN_MIN. Thus 14/14/15/14 gives
0.17 / 0.20. The assistant refuses recommendations with armed spin >=20% or
minimum spin >20%; the existing confirmed minimum-spin writer now allows exactly
20% to support this bench workflow. Firmware metadata limits still apply.

A separate review dialog confirms both parameter values. The existing write/readback
service is reused. If the new armed spin would exceed the current minimum, the
minimum is raised first; otherwise armed spin is written first. Writes are not
atomic: failure/cancellation can leave an already-confirmed earlier write applied,
so refresh values before retrying. No automatic rollback is attempted.

Cancel stops the run and requests motor stop; declining the apply dialog writes
nothing. Disconnect, arming, frame changes, or output remapping prevent further
pulses/writes in that run. Deactivating the view cancels the assistant.
Ordinary motor-test/spin controls are disabled while the assistant owns its run.

Validation covers highest-threshold calculation, the 0.17/0.20 result, required
acknowledgement, cancel/no-write behavior, six-motor frames, and confirmed
readback-backed writes. Physical motor testing has not been performed.

Assistant observations and recommendations are hidden when their original vehicle disconnects, changes, or its connection lifetime is cancelled. A new run is required for the new vehicle context.

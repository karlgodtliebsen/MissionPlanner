# Motor output diagnostics

Open Initial Setup > Optional Hardware > Motor Test and expand Motor output diagnostics.
The existing motor view/viewmodel uses the shared MotorLayoutResolver and IMotorOutputResolver.
The summary is read-only and updates with parameter, vehicle and test changes.

Downloaded frame, output-function, protocol, spin, disarm-safety and board-safety values remain visible
even when the frame is unsupported. Missing values are labelled unavailable. RC options equal to 32
identify configured motor interlocks; they do not establish the current switch state.

Motor rows join logical number, test order, rotation, frame position and physical output assignments.
Duplicate assignments remain ambiguous. Positions are derived from the catalog's roll/pitch factors;
the factors remain visible for unusual layouts. Timer groups are unavailable without board-specific
metadata. The selected protocol does not prove effective output capabilities or that a required reboot occurred.

A successful command acknowledgement is not evidence of rotation. The threshold assistant's explicit
user observations identify which motor paths operated. Guidance for stopped armed motors and for all-motor
test failures is conditional; one parameter or failed command never establishes the diagnosis.

Protocol values follow [ArduPilot motor parameter definitions](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Motors/AP_MotorsMulticopter.cpp).
The interlock assignment follows [ArduPilot rotor speed setup](https://ardupilot.org/copter/docs/traditional-helicopter-rsc-setup.html).

Tests: MotorOutputDiagnosticsTests and MotorOutputResolverTests cover Motor1-4 reassignment,
configuration evidence, unknown fields and conditional diagnostic text. No physical motor verification was performed.

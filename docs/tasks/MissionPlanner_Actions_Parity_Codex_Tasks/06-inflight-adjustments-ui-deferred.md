# In-flight adjustments UI deferral

Task 06 is intentionally deferred because Task 05 could not safely publish its typed
backend. Placeholder controls or raw MAVLink/parameter bindings would violate the package's
architecture rules.

When the prerequisites in `05-inflight-adjustments-investigation.md` are complete, the UI
must state the selected speed type, label altitude as an absolute HOME-relative target, and
describe Set Loiter Radius as a persistent vehicle-parameter change. Each control must bind
to an independently gated typed operation.

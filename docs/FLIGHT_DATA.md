# Flight Data

## Architecture and lifecycle

Flight Data tabs use `LifecycleTabView` with `TabViewLifecycleContent<TViewModel>`. A tab
creates one transient view model when selected and disposes it when it is left. View models
own subscriptions and cancellation for that lifetime. Disposal cancels work and releases
subscriptions; it does not clear UI-bound collections.

## Preflight

Preflight is conservative operator assistance, not a declaration that an aircraft is safe
to fly. It projects promoted immutable vehicle state into explainable checks with a stable
key, category, status, evidence source and timestamp, summary, and remediation. Statuses are
`Pass`, `Warning`, `Fail`, `Stale`, and `NotAvailable`; missing evidence is never treated as
a pass. Overall status is the highest actionable severity.

Heartbeat evidence is stale after five seconds. GPS, power, system-health, and estimator
evidence is stale after ten seconds. The initial rules cover connection, firmware identity,
armed state, GPS, battery, sensor masks, and EKF health. Home, fence, and storage/logging are
reported as unavailable until cohesive state is promoted.

“Run pre-arm checks” sends typed `MAV_CMD_RUN_PREARM_CHECKS` through the acknowledged,
operation-gated command path. It requires an online, disarmed vehicle, is disabled during
telemetry replay, supports cancellation/disconnect, and captures bounded recent
`STATUSTEXT` diagnostics. An ACK means the request was accepted; the resulting diagnostics
remain separate evidence.

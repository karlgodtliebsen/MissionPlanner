# Vehicle Scripts

MissionPlanner vehicle scripts are constrained, versioned JSON automation documents. They
never execute arbitrary source code.

```json
{
  "version": 1,
  "name": "Hold after connection",
  "steps": [
    { "action": "waitForConnection", "arguments": {}, "timeoutSeconds": 30 },
    { "action": "notify", "arguments": { "message": "Vehicle connected" }, "timeoutSeconds": 5 },
    { "action": "hold", "arguments": {}, "timeoutSeconds": 15 }
  ]
}
```

Documents require version `1`, a non-empty name, and 1–100 steps. Each step has an
allow-listed `action`, string-valued `arguments`, and a timeout from 1 to 300 seconds.
`delay` additionally requires `milliseconds` from 0 to 60000. Execution is sequential and
stops at the first failure. Vehicle actions re-check the active connection and execute via
the same typed services and operation gate as their direct UI workflows.

Allowed version-1 actions are `notify`, `delay`, `waitForConnection`, `arm`, `disarm`,
`land`, `rtl`, `hold`, and `auxFunction`. The last requires an `id`; unknown or hazardous
IDs remain blocked by auxiliary policy. Scripts cannot access files, network, processes,
reflection, arbitrary MAVLink commands, or dynamic code, and cannot loop.

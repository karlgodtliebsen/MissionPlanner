# TASK 04 — Dedicated Logs Root Page and Navigation

## Goal

Add a dedicated application-level Logs page so logging tools do not further crowd Flight Data.

## UX decision

Create root navigation destination:

```text
Logs
```

with secondary navigation:

```text
Logs
├── Telemetry
└── Application
```

Prefer `Logs` unless an existing root-level `Diagnostics` page already serves this role.

Do not add Application Logs as another Flight Data tab.

## Existing view reuse

Reuse and host:
- `TelemetryLogsTabView`
- `TelemetryLogsTabViewModel`

Do not duplicate them.

## New host

Add equivalents of:

```text
LogsView
LogsViewModel
```

Resolve subviews/ViewModels using existing DI/navigation conventions.

## Navigation behavior

- direct root navigation access;
- default subview = Telemetry;
- remember last selected subview during session;
- navigating away does not stop logging services;
- hidden views should not keep unnecessary UI subscriptions alive;
- Browser target works.

Optional: a lightweight `Open Telemetry Logs` command from Flight Data may navigate to the root Logs page, but must not add another crowded tab.

## Tests

Cover:
- root nav entry;
- default Telemetry subview;
- switching Telemetry/Application;
- state survives normal navigation away/back;
- Browser resolves page without desktop-only services.

## Acceptance criteria

- Logs is a root page.
- Telemetry viewer is hosted there.
- Application viewer has a host location.
- Flight Data tab count does not increase.
- Desktop and Browser build.

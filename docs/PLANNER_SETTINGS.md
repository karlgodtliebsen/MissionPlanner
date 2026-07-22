# Planner application settings

The Config > Planner page owns local MissionPlanner behavior. It does not read or write
flight-controller parameters.

## Inventory and ownership

Before Config task 06, local behavior came from several independent sources:

| Area | Previous source | Typed Planner section |
| --- | --- | --- |
| Connection channel, host, port, baud | `appsettings.json` and `ApplicationStateService` | `Connection` |
| Logging threshold and file retention | `appsettings.json` / Serilog startup | `Logging` |
| Theme | transient MAUI `UserAppTheme` state | `Appearance` |
| Map source and point zoom | map view-model selection and a hard-coded resolution | `Map` |
| Units | no shared local preference | `Units` |
| Telemetry UI rates/history | no shared local preference | `Telemetry` |
| Parameter cache behavior | no shared local preference | `ParameterCache` |
| Hazardous-operation prompts | feature-local behavior | `Confirmations` |
| Update checks/channel | no shared local preference | `Updates` |
| Telemetry accessibility | no shared local preference | `Accessibility` |

`appsettings.json` remains the packaged bootstrap configuration for logging providers,
available connection choices, firmware manifest configuration, and service startup. User
choices are stored by `IPlannerSettingsService` and take precedence for the settings listed
above.

## Persistence and migration

The service serializes one schema-versioned JSON snapshot through MAUI Preferences. Every
load, save, and import is validated. A document from an older supported schema is migrated
and rewritten at the current version. Corrupt, unsupported, or invalid persisted data is
replaced with safe defaults and reported to the page instead of preventing application
startup.

Import is atomic: validation failure leaves the current snapshot unchanged. Export uses
invariant JSON and contains only the typed non-secret sections. Unknown fields are ignored,
which also prevents a `password`, token, or other secret-shaped import field from being
written back to an export.

Credentials and tokens must use `IPlannerSecretStore`, whose MAUI adapter delegates to
platform SecureStorage. They must not be added to `PlannerSettings`, Preferences, exports,
or structured log properties.

## Live and restart-bound behavior

Saved settings raise one observable change event containing previous/current immutable
snapshots and the affected restart-bound sections.

* Theme changes preview immediately and persist through the same settings service.
* Connection defaults update the shared connection UI only while disconnected; saving
  preferences never interrupts an active vehicle connection.
* Map default zoom is used by point-centering actions. Provider/style is selected when a
  map view is created.
* Map provider/style, logging changes, and update-channel changes are marked as requiring
  restart.
* Telemetry rates, cache policy, confirmation policy, update checks, and accessibility are
  observable application policy for feature consumers; the Planner page does not emulate
  missing consumers or write vehicle parameters.

Each section can be reset independently, and Reset all restores a complete validated
default snapshot after confirmation.

# MissionPlanner Next Gen — DI Registration Cleanup

Execute these tasks one at a time, in order.

## Goal

Clean up non-obvious DI registrations in `ApplicationConfigurator` and related configurators so runtime selection of views/tools is explicit, typed, testable, and lifetime-safe.

## Architectural direction

Prefer:

- keyed services for runtime selection;
- typed factories for activation;
- metadata registrations for discovery/listing;
- constructor injection for ordinary dependencies;
- explicit service lifetimes;
- no ad-hoc `Func<int, Control>`;
- no singleton metadata objects that capture `IServiceProvider`;
- no service-locator use in ViewModels/domain/application services.

`IServiceProvider` remains acceptable at explicit composition/factory boundaries.

## Task order

1. `TASK_01_Audit_DI_Registrations_And_Lifetimes.md`
2. `TASK_02_Refactor_Logs_View_Selection_To_Keyed_Services.md`
3. `TASK_03_Refactor_Advanced_Tools_To_Metadata_And_Keyed_Factory.md`
4. `TASK_04_Remove_ServiceProvider_Captures_And_Lifetime_Hazards.md`
5. `TASK_05_DI_Regression_Tests_And_Documentation.md`

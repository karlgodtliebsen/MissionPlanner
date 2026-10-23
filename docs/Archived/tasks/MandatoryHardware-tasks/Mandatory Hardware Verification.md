# Mandatory Hardware Verification

Verification performed for Task 10:

- `dotnet build src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/MissionPlanner.AvaloniaUI.App.csproj --no-restore`
  succeeds with no errors. The repository continues to emit its existing warning
  backlog; the new canonical workflow-key members and public types have XML
  documentation.
- Focused Mandatory Hardware and setup catalog tests pass: 12 passed, 0 failed.
  These cover ordered navigation definitions, capability-aware catalog behavior,
  completion invalidation, shell connection transitions, tuning calculations and
  boundaries.
- Dependency-graph assertions resolve `IFailSafeService`,
  `IInitTuneParametersService`, `IHwIdService`, `IAdsbService`, and their four
  transient ViewModels. Application-data paths are supplied through the registered Avalonia
  platform adapter so portable test hosts can substitute a deterministic location.
- XAML source generation completes for all four new lifecycle views, which checks
  their types, namespaces, bindings, and code-behind integration at build time.
- Simulator/manual vehicle verification was not available in this non-interactive
  build environment. Runtime behavior remains parameter/capability driven and is
  not restricted to Copter except where the legacy Initial Tune workflow requires
  Copter or QuadPlane semantics.

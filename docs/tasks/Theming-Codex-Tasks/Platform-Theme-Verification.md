# Platform theme verification

Verification was performed on 2026-08-20 with the installed .NET 10 workloads.

| Platform head | Command/result |
| --- | --- |
| WinUI x64 | `dotnet build MissionPlanner.WinUI.csproj --no-restore -p:RuntimeIdentifierOverride=win-x64` — succeeded, 0 warnings, 0 errors. |
| Android | `dotnet build MissionPlanner.Droid.csproj --no-restore` — succeeded, 0 warnings, 0 errors. |
| Mac Catalyst | `dotnet build MissionPlanner.Mac.csproj --no-restore` — succeeded, 0 warnings, 0 errors. |

The shared platform contract is source- and unit-verified:

- Mission Light selects a Light native fallback;
- Mission Dark selects a Dark native fallback;
- Mission Blue selects a Light native fallback while its semantic resources continue
  to come from the distinct Mission Blue palette;
- System leaves `UserAppTheme` unspecified and follows requested OS appearance;
- UraniumUI receives the same base appearance while MissionPlanner overrides use the
  active semantic dynamic resources.

The WinUI build initially required an explicit x64 runtime identifier because the head
declares three Windows runtime identifiers. Parallel head builds also contended for
shared intermediate files, so the recorded results are the clean sequential builds.

This Windows host can compile all three heads. Device/emulator launch and subjective
native-control smoke inspection remain release-environment checks for Android and Mac
Catalyst.

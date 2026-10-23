# Runtime theme switching verification

The automated regression sequence is:

```text
Mission Dark
Mission Light
Mission Blue
Mission Dark
Mission Blue
Mission Light
```

`ThemeManagerTests.RepeatedSwitchingKeepsResourceAndSubscriptionCountsStable`
executes that sequence against the real `ThemeManager` with deterministic palettes.
After every switch it verifies:

- the active dictionary contains exactly one value for every semantic color role;
- the operating-system appearance subscription count remains one;
- every switch loads and applies one complete palette;
- switching completes without exceptions or partial dictionaries.

The broader source coverage was also checked for the disconnected Shell, Mandatory
Hardware, Optional Hardware, Preferences, Full Parameters, DataGrid, and Flight Data:
their MissionPlanner-owned XAML uses dynamic semantic resources, so visible controls
share the same active application dictionary without a restart.

Connected SITL rendering and subjective stale/mixed-color inspection require an
interactive vehicle and platform window. They are retained as release smoke checks;
this non-interactive run does not claim those manual observations were performed.

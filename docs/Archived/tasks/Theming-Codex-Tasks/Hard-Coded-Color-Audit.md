# Hard-coded color audit

MissionPlanner application XAML was searched for hexadecimal literals and the named
colors White, Black, Red, Green, and OrangeRed. Concrete theme dictionaries and the
bootstrap palette are palette definitions and are intentionally excluded from the
view audit.

## Converted application chrome

- map instruction overlays use `InverseSurface` and `InverseOnSurface`;
- the elevation-profile panel and mission-item page use surface-container roles;
- flight-data splitters use `OutlineVariant` and `Primary` for hover;
- map loading indicators use `Primary`;
- the firmware status bar uses `SurfaceContainer`;
- the advanced-command warning border uses `Warning`.

## Retained domain visualization colors

- `HudView.axaml` keeps a black instrument background because it is part of the HUD
  presentation, not surrounding application chrome;
- `OnboardOsdPreviewView.axaml` keeps black and gray because it previews the vehicle's
  on-screen-display output rather than the MissionPlanner theme.

No remaining direct color literal in a normal MissionPlanner view represents
application chrome. Palette literals, compatibility primitives in `Colors.axaml`, and
the two domain visualizations above are legitimate constants.

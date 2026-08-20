# UraniumUI theme audit

This audit covers the MissionPlanner-owned integration boundary. UraniumUI may use
MAUI's light/dark appearance internally; `ThemeManager` supplies that native base
appearance while MissionPlanner overrides application-facing colors with semantic
dynamic resources.

| Control | MissionPlanner integration | Theme behavior |
| --- | --- | --- |
| `SelectField` | Hosts UraniumUI `Select` | Dropdown surface, outline, selection, hover, and pressed colors come from `Surface`, `Outline`, `PrimaryContainer`, and `SurfaceVariant`. |
| `PickerField` | Hosts picker/select presentation | Inherits the semantic `Select` dropdown override and application label/input styles. |
| `TextField` | Uses application label/input styles | Foreground, background, outline, focus, and disabled colors resolve through the active semantic palette. |
| `CheckBox` | Uses the application control styles plus UraniumUI base behavior | Accent and text colors resolve from semantic application styles; MAUI base appearance remains the native fallback. |
| `TabView` / `TabItem` | MissionPlanner supplies tab-header views | Header code uses `SetDynamicResource` for selection, foreground, background, and outline colors. No light/dark branch remains. |
| `Button` | Application override cascades over `UraniumUI.Styles.Button` | Normal and disabled foreground/background colors use `OnPrimary`, `Primary`, `DisabledText`, and `DisabledBackground`. Uranium elevation resources are preserved. |
| `Select` | Explicit application override | All important dropdown state colors use semantic dynamic resources. |
| `DataGrid` | Local VirtualizedDataGrid style integration | Grid surface, separators, stroke, and selection use semantic dynamic resources. The obsolete color-copy workaround is addressed separately by Task 20. |

Source audit results:

- MissionPlanner-owned XAML contains no `AppThemeBinding` color selection.
- MissionPlanner-owned Uranium overrides contain no theme-suffixed resource keys.
- Mission Blue reaches the controls through the same semantic keys as Mission Light
  and Mission Dark; its MAUI base appearance remains Light only for native fallback.

Interactive switching and platform-specific rendering are covered by the runtime and
platform verification tasks rather than asserted by this source audit.

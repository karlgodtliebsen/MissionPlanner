# Platform theme verification

The Avalonia application currently targets Windows. Linux and macOS verification is deferred
until those application heads and packages exist.

| Check | Windows |
|---|---|
| Solution and AXAML compilation | Required |
| Default follows operating-system appearance | Required |
| Light and dark variants | Required |
| Semi variants from `AvaloniaThemeCatalog` | Required |
| Selection persists after restart | Required |
| Ursa drawer, menus, dialogs, and notifications | Required |
| Title-bar buttons and pointer hit testing | Required |
| Warning classes and `ProgressRing` | Required |
| Virtualized grid selection and hover states | Required |

Run the checks interactively after theme catalog, shared style, title-bar, navigation, or
settings-persistence changes. Record unsupported future targets as not available rather than
claiming a successful build.

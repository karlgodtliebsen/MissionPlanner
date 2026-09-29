# Desktop application icon

The existing artwork is in `src/UI/MissionPlanner.App/Resources/AppIcon`.
`mpdesktop.png` remains available for in-app images. `mpdesktop.ico` contains
16, 24, 32, 48, 64, 128, and 256 pixel frames for Windows shell scaling.
To regenerate it after updating the PNG, run `./scripts/Generate-AppIcon.ps1`
on Windows. The generated ICO is committed; normal builds need no conversion tools.

The desktop executable project sets `ApplicationIcon`. This embeds the icon in
the executable used by Explorer and ordinary Windows shortcuts. Setting the
Avalonia main window's `Icon` alone does not set the executable icon.
The main window and detached Diagnostics window also use the same ICO resource.
The ICO is copied beside the executable during build and publish.

For Windows shortcuts:

- Point an ordinary shortcut at the built or published `MissionPlanner.Desktop.exe`.
  It can use the executable's default icon.
- If a shortcut runs `dotnet run`, PowerShell, or another launcher, Windows sees
  that launcher's icon. In shortcut Properties → Change Icon, select the
  `mpdesktop.ico` beside the published executable (or the source asset for a
  development shortcut).
- Existing shortcuts may explicitly reference another icon or retain a cached
  one. Re-select the new executable/ICO in Change Icon or recreate the shortcut.
  Updating the build does not rewrite existing shortcuts or pinned entries.

This configuration covers the Windows desktop executable. Browser favicons and
mobile application icons belong to their respective platform packaging projects.

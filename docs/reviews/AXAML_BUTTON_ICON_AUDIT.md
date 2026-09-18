# AXAML button icon audit

Date: 2026-09-18. Scope: all 131 AXAML files under src, including views, templates, samples, and styles.

Inspected 356 Button-family elements using parsed XML, excluding comments and icons in non-content properties such as flyouts/resources. Found 64 without an explicit icon in their content: 60 text buttons and four Unicode pager glyph buttons. Added MaterialIcon content to 61; three text links remain intentionally unchanged.

Existing Material.Icons.Avalonia controls and ActionIcon styling are reused, with 20-pixel sizing for inline labels and compact pager controls. Labels, commands, parameters, enablement, visibility, tooltips, and warning styling are preserved. Accessible names are explicit. Pause/play labels remain bound; the combined PlayPause icon represents both states.

## Complete findings

Locations below use the original line numbers before icon insertion. Paths are relative to the repository root.

| File | Original line | Content | Outcome |
| --- | ---: | --- | --- |
| [src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml](../../src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml) | 123 | ⏮ | Added PageFirst |
| [src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml](../../src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml) | 124 | ◀ | Added ChevronLeft |
| [src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml](../../src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml) | 137 | ▶ | Added ChevronRight |
| [src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml](../../src/UI/MissionPlanner.App/Resources/Styles/VirtualizedItemsGrid.axaml) | 138 | ⏭ | Added PageLast |
| [src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceMapView.axaml](../../src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceMapView.axaml) | 45 | {Binding AttributionText} | Retained: map-provider legal attribution is a text link. |
| [src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml](../../src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml) | 95 | Inclusion polygon | Added VectorPolygon |
| [src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml](../../src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml) | 96 | Exclusion polygon | Added VectorPolygon |
| [src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml](../../src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml) | 98 | Inclusion circle | Added CircleOutline |
| [src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml](../../src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceTabView.axaml) | 99 | Exclusion circle | Added CircleOutline |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 25 | Arm | Added ShieldCheck |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 26 | Disarm | Added ShieldOff |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 27 | RTL | Added HomeMapMarker |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 28 | Loiter / Hold | Added Pause |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 29 | Land | Added AirplaneLanding |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 38 | Set Mode | Added Tune |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 50 | Takeoff | Added AirplaneTakeoff |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 60 | {Binding AltitudeZeroActionText} | Added CrosshairsGps |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 77 | Set Current WP | Added MapMarker |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 82 | Restart Mission | Added Restart |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 86 | Resume Mission | Added Play |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 91 | Abort Landing | Added AirplaneTakeoff |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.axaml) | 121 | Set Loiter Radius | Added CircleExpand |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/MessagesTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/MessagesTabView.axaml) | 16 | {Binding PauseButtonText} | Added PlayPause |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/ServoRelayTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/ServoRelayTabView.axaml) | 46 | Set servo | Added Tune |
| [src/UI/MissionPlanner.App/Views/FlightData/Tabs/TelemetryLogsTabView.axaml](../../src/UI/MissionPlanner.App/Views/FlightData/Tabs/TelemetryLogsTabView.axaml) | 31 | {Binding PlayPauseText} | Added PlayPause |
| [src/UI/MissionPlanner.App/Views/InitSetup/Advanced/Warnings/WarningRuleListView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/Advanced/Warnings/WarningRuleListView.axaml) | 34 | Enable / disable | Added ToggleSwitchOutline |
| [src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/InstallFirmwarePage.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/InstallFirmwarePage.axaml) | 207 | Enter DFU using BOOT button | Added Usb |
| [src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/AccelerometerSetupView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/AccelerometerSetupView.axaml) | 23 | Start six-position calibration | Added Play |
| [src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/EscMotorSetupView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/EscMotorSetupView.axaml) | 54 | Test motor | Added Fan |
| [src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/EscMotorSetupView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/EscMotorSetupView.axaml) | 55 | Test sequence | Added PlaylistPlay |
| [src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/EscMotorSetupView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/EscMotorSetupView.axaml) | 56 | STOP | Added Stop |
| [src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupView.axaml) | 36 | Start calibration | Added Play |
| [src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/SerialPortsView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/SerialPortsView.axaml) | 64 | Reload reported parameters | Added Refresh |
| [src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/SerialPortsView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/SerialPortsView.axaml) | 65 | Apply changes | Added Check |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CompassMotorCalibrationView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CompassMotorCalibrationView.axaml) | 27 | Start CompassMot | Added Play |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CompassMotorCalibrationView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CompassMotorCalibrationView.axaml) | 28 | Stop | Added Stop |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/JoystickView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/JoystickView.axaml) | 31 | Disable and release vehicle control | Added Stop |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 53 | Start — props removed | Added Play |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 54 | Pulse selected motor (1s) | Added Fan |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 55 | Increase by 1% | Added Plus |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 56 | Rotates reliably — next motor | Added Check |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 57 | Cancel and stop | Added Stop |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 58 | Review and apply both values | Added CheckAll |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 89 | {Binding Label} | Added Fan |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 98 | Test all Motors | Added Fan |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 99 | Test all in sequence | Added PlaylistPlay |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 101 | STOP All Motors | Added Stop |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 120 | Set Motor Spin Arm | Added ContentSave |
| [src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml](../../src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml) | 132 | Set Motor Spin Min | Added ContentSave |
| [src/UI/MissionPlanner.App/Views/Introduction/Views/IntroductionTopicView.axaml](../../src/UI/MissionPlanner.App/Views/Introduction/Views/IntroductionTopicView.axaml) | 33 | {Binding Label} | Retained: shared dynamic action can navigate to a topic, route, URI, or back; no single accurate action icon. |
| [src/UI/MissionPlanner.App/Views/Missions/MissionMapView.axaml](../../src/UI/MissionPlanner.App/Views/Missions/MissionMapView.axaml) | 88 | {Binding AttributionText} | Retained: map-provider legal attribution is a text link. |
| [src/UI/MissionPlanner.App/Views/Samples/DataGridPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DataGridPage.axaml) | 27 | Load sample data from json file | Added FolderOpen |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 53 | Connect | Added PowerPlug |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 64 | Error | Added AlertCircleOutline |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 75 | Confirm | Added CheckCircleOutline |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 83 | ShowConfirm1 | Added CheckCircleOutline |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 93 | ShowStringPrompt | Added FormTextbox |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 102 | ShowStringPrompt2 | Added FormTextbox |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 112 | ShowIntPrompt | Added Numeric |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 121 | ShowIntPrompt2 | Added Numeric |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 130 | ShowDoublePrompt | Added Numeric |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 139 | ShowDoublePrompt2 | Added Numeric |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 147 | ShowChoosePrompt | Added FormatListBulleted |
| [src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml](../../src/UI/MissionPlanner.App/Views/Samples/DialogDemoPage.axaml) | 156 | ShowPhrasePrompt | Added TextBoxOutline |

## Remaining without icons

- src/UI/MissionPlanner.App/Views/Missions/MissionMapView.axaml:88 — Retained: map-provider legal attribution is a text link.
- src/UI/MissionPlanner.App/Views/Introduction/Views/IntroductionTopicView.axaml:33 — Retained: shared dynamic action can navigate to a topic, route, URI, or back; no single accurate action icon.
- src/UI/MissionPlanner.App/Views/ConfigTuning/Tabs/GeoFenceMapView.axaml:45 — Retained: map-provider legal attribution is a text link.

## Verification

- Parsed all AXAML again: 356 buttons, exactly three remaining without content icons.
- Compared all 112 buttons in changed files against HEAD: existing attributes and text/bound labels preserved (pager glyphs replaced with equivalent icons).
- UI project build passed with 0 errors and 11 existing warnings; compiled AXAML and bindings validated.
- Full solution build exposed a pre-existing test fixture constructor mismatch in ParameterNotificationThreadingTests. Test repairs and full-suite results are tracked separately under Task 2.
- No interactive visual review was performed.

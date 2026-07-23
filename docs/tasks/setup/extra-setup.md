# Setup Extra. Split Mandatory Hardware into self-owned setup views

**Implementation status:** Completed 2026-07-23.

## Objective

Replace the workflow-specific layout blocks embedded in `MandatoryHardwareView.xaml` with
dedicated MAUI views. Each child view must resolve, bind, and own its own ViewModel. The
Mandatory Hardware shell must only select and show the active workflow and coordinate
cross-cutting page concerns.

## Ownership boundary

- `MandatoryHardwareView` resolves and binds `MandatoryHardwareViewModel`.
- `MandatoryHardwareViewModel` owns workflow selection, child visibility, the connected
  vehicle heading, summary reporting, manual completion, and navigation to Config pages.
- Every workflow view resolves its own transient ViewModel through `ServiceHelper`, assigns
  it to `BindingContext`, and owns its activation/deactivation lifecycle.
- Every workflow ViewModel owns its commands, status, progress, error state, subscriptions,
  cancellation, and domain-service calls.
- `SetupSectionView` provides the shared view-lifecycle bridge. It activates the owned
  ViewModel only while the child view is both loaded and visible, and deactivates it when
  hidden or unloaded.
- Active child ViewModels reload their own state after vehicle, connection, or firmware
  identity boundaries. Configuration-only sections ignore telemetry-only vehicle changes;
  the radio, battery, and servo sections retain their intentional live updates.
- The parent never creates, retains, supplies, or disposes child ViewModels. The former
  setup ViewModel factory has therefore been removed.
- Child ViewModels are transient DI registrations. Their exact `SetupWorkflowDescriptor`
  is supplied by the registration factory.

The existing active-vehicle and domain services provide the required coordination. No new
event-hub event or Channel is needed for this UI composition; Channels remain reserved for
streaming pipeline communication.

## View construction pattern

Each child view follows the standard MAUI ownership pattern:

```csharp
private readonly AViewModel viewModel;

/// <summary>
/// Provides the public API for AView.
/// </summary>
public AView()
{
    InitializeComponent();
    viewModel = ServiceHelper.GetRequiredService<AViewModel>();
    BindingContext = viewModel;
    OwnedViewModel = viewModel;
}
```

The Mandatory Hardware page composes these self-owned views and binds only their
`IsVisible` properties to shell selection state.

## Implemented sections

- Firmware
- Frame
- Accelerometer
- Compass
- Radio
- Flight Modes
- Battery
- ESC/Motor
- Servo Output
- Optional Hardware
- Safety
- Setup Summary

## Repository constraints

- Work only under `src/`, `docs/`, `scripts/`, and test-data folders belonging to the new
  solution.
- Treat `src-v.1.38/` as read-only reference material.
- Preserve the existing architecture: wire protocol in `MissionPlanner.MavLink`, transport
  in `MissionPlanner.Transport`, application/domain behavior in `MissionPlanner.Core`, and
  MAUI presentation in `MissionPlanner.App`.
- Do not call MAVLink transports directly from views or code-behind. Use injected
  application/domain services from ViewModels.
- Keep code-behind limited to view ownership, lifecycle, and unavoidable UI integration.
- Vehicle-changing operations must be connection-aware, cancellation-aware, target the
  active `VehicleId`, and expose acknowledgement or an explicit failure state.
- All public types and members require XML documentation.
- Every MAUI XAML `Button` must explicitly set `BackgroundColor="Transparent"` and
  `FontSize="14"`.

## Verification

- The focused Setup, lifecycle, calibration, frame, compass, and firmware tests pass:
  51 passed, 0 failed.
- The sequential `MissionPlanner.slnx` build passes.
- The affected App build reports no CS1591 or CS1587 warnings.
- The structural XAML audit confirms every affected `Button` explicitly declares the
  required background and font size.
- The full Core suite remains at 419 passed, 4 skipped, and the 12 unrelated environment
  and integration failures recorded in [`docs/KNOWN_TEST_FAILURES.md`](../../KNOWN_TEST_FAILURES.md).

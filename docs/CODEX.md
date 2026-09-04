CODEX.md



Architecture

------------

- Domain Driven Design

- Never bypass domain services

- No business logic in UI

- Use EventHub for domain events



More documentation in DESIGN\_CONCEPTS.md and ARCHITECTURE\_DECISION\_RECORDS.md





Coding Style

------------	

- Follow .editorconfig
- File-scoped namespaces

- Primary constructors
- Records where appropriate



Testing

-------

- xUnit
- FluentAssertions
- FakeMavLinkVehicle for integration tests



Current Priorities

------------------

1. Mission subsystem

2. Vehicle Service

3. Map

4. Waypoints

5. Flight Planner



Do Not

-------
- Add static state
- Add service locators
- Introduce anemic domain models

- Edit MAVLink files under `src/Core/MissionPlanner.MavLink/Generated/` or the generated
  promotion catalog manually. Use `scripts/Generate-MavLinkDialect.ps1` and follow
  `docs/MAVLINK.md`.

- Download MAVLink dialect data during normal builds or tests. The pinned vendored inputs
  and generation manifest are the authoritative offline source.




Important Exception for Service Locator pattern:

-------

Avalonia views that inherit the generic application base classes are parameterless. The base
class resolves the registered ViewModel and assigns `DataContext`; do not repeat that work in
individual code-behind files.


```csharp


/// <summary>

/// Represents the view for flight planning.

/// </summary>

public partial class FlightPlannerPage : NavigationViewBase<FlightPlannerViewModel>

{

	   /// <summary>
	
	   /// Initializes a new instance of the <see cref="FlightPlannerView"/> class.
	
	   /// </summary>
	
   public FlightPlannerPage()
	
	   {
	
	       InitializeComponent();
	
	   }

}
```

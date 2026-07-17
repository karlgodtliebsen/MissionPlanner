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




Important Exception for Service Locator pattern:

-------

Using .Net MAUI, to support Design time views, the ServiceLocator pattern is used in Views contructors, see sample below


```csharp


/// <summary>

/// Represents the view for flight planning.

/// </summary>

public partial class FlightPlannerView : UraniumContentPage

{

	   /// <summary>
	
	   /// Initializes a new instance of the <see cref="FlightPlannerView"/> class.
	
	   /// </summary>
	
	   public FlightPlannerView()
	
	   {
	
	       InitializeComponent();
	
	       var viewModel = ServiceHelper.GetRequiredService<FlightPlannerViewModel>();
	
	       BindingContext = viewModel;
	
	   }

}
```






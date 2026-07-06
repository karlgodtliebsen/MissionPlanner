# .NET MAUI Shell with Persistent Top and Bottom Bars

This document explains how the MissionPlanner application implements persistent UI chrome (top bar and bottom status bar) across all pages using .NET MAUI Shell.

## Architecture Overview

```
Window
└── ContentPage (implicit wrapper created by MAUI)
	└── AppShell
		├── Shell.TitleView (Persistent Top Bar)
		├── ShellContent Pages (your actual pages)
		└── (Bottom bar added per-page, see below)
```

## Components

### 1. Persistent Top Bar (`Shell.TitleView`)

**Location**: `src/UI/MissionPlanner.App/AppShell.xaml`

The top bar is defined in `AppShell.xaml` using `<Shell.TitleView>`:

```xaml
<Shell.TitleView>
	<Grid ColumnDefinitions="Auto,*,Auto,Auto" 
		  Padding="8,0" 
		  BackgroundColor="{AppThemeBinding Light={StaticResource Surface}, Dark={StaticResource SurfaceDark}}"
		  HeightRequest="50">

		<!-- App Title/Logo -->
		<Label Grid.Column="0" 
			   Text="MissionPlanner" 
			   FontSize="18" 
			   FontAttributes="Bold"/>

		<!-- Connection Status (Center) -->
		<HorizontalStackLayout Grid.Column="1" 
							   HorizontalOptions="Center">
			<Label Text="●" TextColor="Gray"/>
			<Label Text="Not Connected"/>
		</HorizontalStackLayout>

		<!-- Connect Button -->
		<Button Grid.Column="2"
				Text="Connect"
				Clicked="OnConnectClicked"/>

		<!-- Settings Button -->
		<Button Grid.Column="3" Text="⚙"/>
	</Grid>
</Shell.TitleView>
```

**Features**:
- ✅ Visible across all pages automatically
- ✅ Can contain buttons, labels, and any MAUI controls
- ✅ Can bind to Shell-level ViewModels or singleton services
- ✅ Automatically positioned at the top by Shell

### 2. Persistent Bottom Status Bar

**Location**: 
- `src/UI/MissionPlanner.App/Views/Common/StatusBar.xaml`
- `src/UI/MissionPlanner.App/Views/Common/StatusBarViewModel.cs`

The bottom status bar is a reusable component that can be added to pages:

```xaml
<common:StatusBar 
	BindingContext="{uranium:Inject Type={Type common:StatusBarViewModel}}"
	x:DataType="common:StatusBarViewModel"/>
```

**StatusBarViewModel Features**:
- ✅ Shows current time (updated every second)
- ✅ Shows connection status with colored dot (Gray = disconnected, Green = connected)
- ✅ Shows vehicle connection events from Core domain events
- ✅ Shows status messages
- ✅ Bound to `ApplicationStateService` for connection state
- ✅ Subscribes to `VehicleConnected` domain events

## How to Add Status Bar to a Page

### Option A: Add to Individual Pages (Recommended)

In any page XAML, wrap your content in a Grid with 2 rows:

```xaml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
			 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
			 xmlns:common="clr-namespace:MissionPlanner.App.Views.Common"
			 xmlns:uranium="http://schemas.enisn-projects.io/dotnet/maui/uraniumui"
			 x:Class="MissionPlanner.App.Views.YourPage">

	<Grid RowDefinitions="*,Auto">

		<!-- Your page content -->
		<Grid Grid.Row="0">
			<!-- Your actual UI here -->
		</Grid>

		<!-- Status bar at bottom -->
		<common:StatusBar Grid.Row="1"
						 BindingContext="{uranium:Inject Type={Type common:StatusBarViewModel}}"
						 x:DataType="common:StatusBarViewModel"/>
	</Grid>
</ContentPage>
```

### Option B: Base Page Class (For Many Pages)

If you want all pages to have the status bar without repeating XAML:

1. Create a base page in `src/UI/MissionPlanner.App/Views/BasePage.cs` (already created)
2. Inherit from `BasePage` instead of `ContentPage`
3. The status bar is automatically added

**C# Example**:
```csharp
public class YourPage : BasePage
{
	public YourPage()
	{
		Content = new Grid
		{
			// Your UI here - status bar is automatically below
		};
	}
}
```

### Option C: Update FlightDataView Example

**File**: `src/UI/MissionPlanner.App/Views/FlightData/FlightDataView.xaml`

```xaml
<?xml version="1.0" encoding="utf-8" ?>
<uranium:UraniumContentPage 
	xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
	xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
	xmlns:common="clr-namespace:MissionPlanner.App.Views.Common"
	xmlns:uranium="http://schemas.enisn-projects.io/dotnet/maui/uraniumui"
	x:Class="MissionPlanner.App.Views.FlightData.FlightDataView">

	<!-- Wrap everything in a Grid with status bar at bottom -->
	<Grid RowDefinitions="*,Auto">

		<!-- Existing content goes in Row 0 -->
		<Grid Grid.Row="0" ColumnDefinitions="Auto,5,*">
			<!-- Your existing HUD, tabs, map, etc. -->
		</Grid>

		<!-- Status bar in Row 1 -->
		<common:StatusBar Grid.Row="1"
						 BindingContext="{uranium:Inject Type={Type common:StatusBarViewModel}}"
						 x:DataType="common:StatusBarViewModel"/>
	</Grid>
</uranium:UraniumContentPage>
```

## Binding to Connection State

The `StatusBarViewModel` automatically:

1. **Subscribes to `ApplicationStateService`**:
   - Updates when `IsConnected` changes
   - Shows "Connected" / "Disconnected"

2. **Subscribes to Domain Events**:
   - Listens for `VehicleConnected` events
   - Updates status message with vehicle ID and connection type

3. **Updates Clock**:
   - Uses `IDispatcherTimer` to update every second

### Example: Updating Status from Anywhere

```csharp
// From any ViewModel or service:
public class SomeViewModel
{
	private readonly ApplicationStateService stateService;

	public SomeViewModel(ApplicationStateService stateService)
	{
		this.stateService = stateService;
	}

	public void OnSomeEvent()
	{
		// Update connection state - status bar will automatically reflect this
		stateService.IsConnected = true;
	}
}
```

## Advantages of This Approach

✅ **Shell.TitleView** gives you a persistent top bar across ALL pages automatically
✅ **Reusable StatusBar component** can be added to pages that need it
✅ **Singleton ViewModel** means one instance updates everywhere
✅ **Domain Event Integration** keeps UI in sync with Core connection logic
✅ **Theme-aware** using AppThemeBinding
✅ **Loosely coupled** - pages don't need to know about status bar implementation

## Shell Features Used

- `Shell.TitleView` - Top chrome
- `Shell.FlyoutHeader` - Flyout menu header
- `Shell.FlyoutContent` - Custom flyout content
- Shell navigation - `Shell.Current.GoToAsync()`
- Shell styling - Visual states and theming

## Summary

Your application now has:

1. **Top Bar**: Always visible via `Shell.TitleView` in `AppShell.xaml`
   - Shows app title, connection status, connect button, settings button

2. **Bottom Status Bar**: Opt-in per page via `<common:StatusBar/>`
   - Shows status messages, connection info with colored dot, current time
   - Automatically updates from connection events

3. **Both bars** persist across navigation and integrate with your Core domain events and connection services.

## Next Steps

- Update individual pages to include `<common:StatusBar/>` as shown above
- Optionally bind top bar connection status to live data (requires adding ViewModel binding)
- Customize colors, fonts, and layout to match your design

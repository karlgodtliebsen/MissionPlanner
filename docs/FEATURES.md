\# Domain



\## MavLink

\### Status

* Near Completed

\## Vehicle



\### Status

* Complete Domain Model



\## Messages

\### Status

* Compete set of Messages



\## Mission

\### Status

* In progress. Needs completion











\# UI





FlightPlanner



\## HUD:

\### Status

Implemented a responsive HUD



\### Missing

More details in overlay





\## Tabset

Only Quick Implemented at the moment



\### Quick



\### Status



\### Missing









\## Connection



\### Status



SERIAL/USB implemented and tested



\### Missing

TCP/UDP needs testing

Other







\## Flight Map:



\### Status



A responsive Map

Right Click Menu





\### Missing



Stubbed (menu item present, shows "not implemented yet" toast): 

Insert Spline WP, 

Jump,

DO\_SET\_ROI,

Polygon, 

Geo-Fence, 

Rally Points,

Auto WP (survey etc.), 

Measure/Rotate/Prefetch/KML/Elevation, 

Load WP/KML/SHP, 

POI, 

Tracker Home, 

Enter UTM Coord. 



I dropped "Switch Docking" (WinForms-specific) and "GDAL Opacity" (no GDAL in the new stack). 



Loading WP files needs a protocol→domain reverse mapping (

IMissionProtocolMapper only has ToProtocol) — that's the natural next domain addition, along with Jump/ROI commands in the MissionCommand enum.









\# SETUP







\# CONFIG



\## Full Parameters List



\### Status

* Loads Meta data and Parameters and merges these
* Currently a lot of Buttons for testing purposes. Needs trimming



\### Missing





* Extend validation/range support in param editor
* Write





\### Issues:

Parameter loading is way to slow







\# SIMULATION





\# HELP







\# EXIT

\### Status

Completed








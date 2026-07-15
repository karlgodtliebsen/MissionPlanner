# Mission planning foundation

This folder contains a first vertical slice for local mission editing, validation, MAVLink mapping and mission upload/download.

Start with `Mission`, typed `MissionItem` records and `MissionValidator`. 

The UI should edit these domain objects through an application service rather than storing map pins as the mission model.

`MissionTransferService` implements the MAVLink request/response handshake. 

It is intentionally independent of MAUI and map controls.

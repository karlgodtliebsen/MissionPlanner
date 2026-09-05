The mission-planning page is where a route becomes an ArduPilot mission rather than just points drawn on a map.

## A practical mission workflow

1. Create or load the mission.
2. Add, move, reorder, or remove mission items.
3. Check each command and its parameters.
4. Verify altitude values and altitude references.
5. Review the route on the map and in the mission editor/quick editor.
6. Upload the mission to the connected vehicle.
7. Read back or otherwise verify the vehicle mission when the workflow provides that option.

## Map and editor stay together

The graphical mission route and the editor represent the same mission. Use the map for spatial understanding and the editor for exact values.

A visually reasonable route can still contain an incorrect command, altitude, frame, or sequence. The tabular/editor representation is therefore an essential part of verification.

## Before flight

Uploading a mission is not the same as authorizing it for flight. Confirm home position, flight mode, failsafes, terrain/altitude assumptions, mission sequence, vehicle readiness, and the intended start procedure before execution.

**MissionPlanner Preferences** contains settings for the Ground Control Station application.

These settings should be kept conceptually separate from vehicle configuration:

- MissionPlanner Preferences affect the application and operator experience.
- Vehicle Setup/Configuration affects the connected flight controller.

Typical application preferences can include display units, map behavior and map sources, UI choices, connection defaults, logging behavior, and other workstation-specific options as they are implemented.

This separation is useful when MissionPlanner is used with more than one vehicle: the operator's application preferences can remain stable even though each vehicle has its own parameters and hardware configuration.

# Mission intervention UI deferral

Task 04 is intentionally deferred because Task 03 could not safely publish the complete
typed backend. Adding disabled, placeholder, or Expert-command-backed controls would violate
the task package's UI and architecture rules.

When the backend prerequisites in `03-mission-intervention-investigation.md` are resolved,
the UI must bind only to the typed mission-operation service, show canonical MAVLink mission
sequence numbers, and independently gate Set Current WP, Restart Mission, Resume Mission,
and Abort Landing. Until then the existing core Actions layout remains unchanged.

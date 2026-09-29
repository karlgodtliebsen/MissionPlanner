# Parameter profiles

Open **Configuration → Parameters Editor → parameter actions menu → Parameter profiles**
after loading parameters for the intended vehicle.

## Create and manage

Enter a name, optional description and comma-separated tags. Choose **All parameters**,
**Modified parameters**, or **Selected names**. For a subset, enter exact parameter names
separated by commas, spaces or newlines. Unknown names and empty scopes are reported;
they do not silently create incomplete profiles.

**Create new profile** saves a new local snapshot, including pending edits. It does not
replace the selected saved profile. Firmware family/version, vehicle type and source
identity are captured by the existing `IParameterProfileService`.

Select any saved profile from the dropdown to review it. **Save details** changes its
name, description and tags without replacing its stored values. **Duplicate** copies
stored values under a new identity. **Delete profile** requires confirmation.

**Export profile** writes the versioned profile JSON, including metadata. **Import profile**
accepts this profile format and creates a new local identity; it never silently replaces
an existing profile. Ordinary parameter CSV/JSON save/load remains a separate workflow.

The existing JSON repository stores profiles under
`%LOCALAPPDATA%/MissionPlanner/ParameterProfiles` by default. The `ParameterProfiles.Directory`
setting can override this path. Saves use temporary files and atomic replacement.

## Review and stage

The comparison shows live and stored profile values, units and classification.
Unknown/missing parameters and entries without sufficient metadata remain visible but
cannot be selected as compatible differences. Firmware/version/vehicle mismatch warnings
are shown before staging; matching recorded identity does not prove hardware compatibility.

Select individual compatible rows, or use **Select compatible differences**, then
**Stage selected**. Confirmation explicitly warns that pending edits for those selected
names will be replaced. Current session validity and comparison values are checked again
after confirmation. Rejected values are listed in the result; no profile command calls
the parameter apply/write APIs.

Close the dialog, review pending edits in the Parameters Editor, and use its existing
**Apply modified** workflow when ready. Session changes require reopening the dialog
against the current vehicle. **Refresh comparison** rebuilds the review and clears row
selection without changing pending values.

## Implementation and verification

`ParameterProfilesView` and `ParameterProfilesViewModel` replace the placeholder
`LoadPreSavedCommand` behavior. The view model is created through `IDomainFactory` with
the current `IParameterEditSession`; persistence, compatibility, comparison and staging
reuse existing Core services. File dialogs reuse `IFileOpenService`/`IFileSaveService`.

Offline view-model tests cover creation scopes, metadata persistence, multi-profile
selection, rename/duplicate/delete, import identity isolation, export, invalid inputs,
compatibility warnings, explicit staging, session invalidation during confirmation and
absence of writes. A headless test renders the actual AXAML with production styles.
Existing editor progress/threading tests and Core profile/comparison tests are also run.
No connected flight controller is used for validation.
